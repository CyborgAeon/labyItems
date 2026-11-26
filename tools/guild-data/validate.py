#!/usr/bin/env python3
"""Validate the bundled guild data and report outstanding source questions."""

import argparse
import gzip
import json
from pathlib import Path


REPOSITORY = Path(__file__).resolve().parents[2]
RAW = REPOSITORY / "labyItems/Resources/Raw"
BASELINE = REPOSITORY / "docs/guild-migration/backups/guilds.pre-refactor.json.gz"
TIERS = ("Basic", "Intermediate", "Advanced")
FIELDS = {
    "race", "racesubtype", "peopletype", "class", "bracket", "classbracket",
    "alignment.order", "alignment.moral", "outlawhood", "guild", "guilds",
    "ability", "abilities", "level", "adventure.guildmemberbracket",
    "adventure.unverifiedguildoption",
}
KINDS = {"Grant", "Choice", "Upgrade", "Conditional", "MiracleListAdd"}
MODES = {"Patch", "Replace", "Remove", "Legacy", "LegacyOverwrite", "Review"}
TIMINGS = {"OnJoin", "OnAdventureStart", "OnDayStart", "OnSectionStart", "OnUse", "Battleboard"}


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON member {key!r}")
        result[key] = value
    return result


def read(path):
    opener = gzip.open if path.suffix == ".gz" else open
    try:
        with opener(path, "rt", encoding="utf-8") as source:
            return json.load(source, object_pairs_hook=unique_object)
    except (OSError, ValueError) as exc:
        raise ValueError(f"{path.relative_to(REPOSITORY)} — {exc}") from exc


def nodes(entries, branch=()):
    for entry in entries:
        yield entry, branch
        yield from nodes(entry.get("Grants", []), branch)
        if "Choice" in entry:
            yield from nodes([entry["Choice"]], branch)
        for option in entry.get("Options", []):
            choice_id = entry.get("SelectionRef") or entry["Id"]
            yield from nodes(option.get("Grants", []), (*branch, (choice_id, option["Id"])))
        if "Choice" in entry.get("Replacement", {}):
            yield from nodes([entry["Replacement"]["Choice"]], branch)


class Validator:
    def __init__(self):
        self.errors = []
        self.warnings = []

    def error(self, location, message):
        self.errors.append(f"{location} — {message}")

    def warning(self, location, message):
        self.warnings.append(f"{location} — {message}")

    def dictionary(self, resource, field):
        value = resource.get(field)
        if not isinstance(resource.get("$schemaVersion"), int) or not isinstance(value, dict):
            self.error(field, "Expected a versioned dictionary")
            return {}
        seen = set()
        for key in value:
            token = key.casefold()
            if token in seen:
                self.error(field, f"Duplicate case-insensitive identifier {key!r}")
            seen.add(token)
        return value

    def rules(self, location, rules):
        for rule in rules:
            fields = rule.get("Field", "").casefold().split("|")
            if any(field not in FIELDS for field in fields):
                self.error(location, f"Unknown rule field {rule.get('Field')!r}")
            if rule.get("Operator") not in {"In", "NotIn", "Only"}:
                self.error(location, f"Unknown rule operator {rule.get('Operator')!r}")
            if not isinstance(rule.get("Value"), list):
                self.error(location, f"Invalid rule values for {rule.get('Field')!r}")

    def refs(self, location, entry, abilities, choice_sets, baseline_refs):
        for field in ("AbilityRef", "DescriptionRef"):
            ref = entry.get(field)
            if ref and ref.casefold() not in abilities:
                report = self.warning if ref in baseline_refs else self.error
                report(location, f"Missing ability {ref!r}")
        choice_refs = entry.get("ChoiceSetRefs") or []
        if entry.get("ChoiceSetRef"):
            choice_refs = [*choice_refs, entry["ChoiceSetRef"]]
        for ref in choice_refs:
            if ref.casefold() not in choice_sets:
                self.error(location, f"Missing choice set {ref!r}")

    def guild(self, name, guild, abilities, choices, miracles, baseline_refs):
        self.rules(name, guild.get("Availability", {}).get("Rules", []))
        denomination = guild.get("DenominationalMiracle", {}).get("$ref")
        if denomination and denomination.casefold() not in miracles:
            variants = {f"{denomination.casefold()} {suffix}" for suffix in ("i", "ii", "iii")}
            if not variants <= miracles:
                self.warning(name, f"Unknown denominational miracle {denomination!r}")
        definitions = {}
        active = []
        for tier_index, tier in enumerate(TIERS):
            for entry, branch in nodes(guild.get("Benefits", {}).get(tier, [])):
                identity = entry.get("Id", "").casefold()
                location = f"{name} / {tier} / {entry.get('Id')}"
                if not identity or identity in definitions:
                    self.error(location, "Missing or duplicate guild grant ID")
                definitions[identity] = (entry, branch, tier_index)
                active.append((location, entry, branch, tier_index))
        for location, entry, branch, tier_index in active:
            kind = entry.get("Kind", "Grant")
            if kind not in KINDS:
                self.error(location, f"Unknown grant kind {kind!r}")
            self.refs(location, entry, abilities, choices, baseline_refs)
            self.refs(location, entry.get("Patch", {}), abilities, choices, baseline_refs)
            self.refs(location, entry.get("Replacement", {}), abilities, choices, baseline_refs)
            self.rules(location, entry.get("Rules", []))
            self.number(location, entry, "Count")
            if "Amount" in entry and (
                not isinstance(entry["Amount"], list)
                or not entry["Amount"]
                or any(type(value) is not int for value in entry["Amount"])
            ):
                self.error(location, "Amount must be a nonempty list of integers")
            if kind == "Choice":
                if entry.get("Timing") and entry["Timing"] not in TIMINGS:
                    self.error(location, f"Invalid choice timing {entry['Timing']!r}")
                options = entry.get("Options", [])
                ids = set()
                for option in options:
                    option_id = option["Id"].casefold()
                    if option_id in ids:
                        self.error(location, f"Duplicate choice option {option_id!r}")
                    ids.add(option_id)
                    self.rules(location, option.get("Rules", []))
                self.number(location, entry, "Select")
                allowance = entry.get("Select", 1)
                if (isinstance(allowance, int) and allowance > len(ids)
                    and not entry.get("ChoiceSetRefs")
                    and not any(option.get("Repeatable") for option in options)):
                    self.error(location, "Choice allowance exceeds the number of usable options")
                if any(option.casefold() not in ids for option in entry.get("LegacyOptionIds", [])):
                    self.error(location, "Legacy selection mapping points to an absent option")
                if entry.get("SelectionRef"):
                    self.target(location, entry["SelectionRef"], definitions, tier_index, "Choice", branch)
            if kind == "Upgrade":
                mode = entry.get("Mode")
                if mode not in MODES:
                    self.error(location, f"Unknown upgrade mode {mode!r}")
                elif mode in {"Legacy", "LegacyOverwrite", "Review"}:
                    self.warning(location, f"Upgrade requires migration review ({mode})")
                else:
                    patch = entry.get("Patch", {})
                    target_kind = "Choice" if {"Select", "GrantAll", "OptionOverrides"} & patch.keys() else None
                    if mode == "Replace" or {"Count", "CountDelta"} & patch.keys():
                        target_kind = "Grant"
                    self.target(location, entry.get("TargetGrantId"), definitions, tier_index, target_kind, branch)
                    self.number(location, patch, "Select")
                    self.number(location, patch, "Count")
                    if "CountDelta" in patch and type(patch["CountDelta"]) is not int:
                        self.error(location, "CountDelta must be an integer")
            if kind == "MiracleListAdd" and entry.get("MiracleKey", "").casefold() not in miracles:
                self.warning(location, f"Unknown miracle {entry.get('MiracleKey')!r}")

    def target(self, location, target_id, definitions, tier_index, kind, branch):
        target = definitions.get((target_id or "").casefold())
        if not target:
            self.error(location, f"Missing target {target_id!r}")
        elif target[2] > tier_index:
            self.error(location, f"Target {target_id!r} belongs to a later tier")
        elif kind and target[0].get("Kind", "Grant") != kind:
            self.error(location, f"Target {target_id!r} must be a {kind}")
        elif any(dict(branch).get(choice_id, option_id) != option_id for choice_id, option_id in target[1]):
            self.error(location, f"Target {target_id!r} belongs to a mutually exclusive option")

    def number(self, location, node, field):
        value = node.get(field, 0)
        if type(value) is not int or value < 0:
            self.error(location, f"{field} must be a nonnegative integer")

    def inheritance(self, abilities):
        pending, resolved = set(), set()

        def visit(key):
            if key in pending:
                self.error(key, "Circular ability inheritance")
                return
            if key in resolved:
                return
            pending.add(key)
            for ref in abilities[key].get("RuleReferences", []):
                if ref.get("Catalogue", "").casefold() != "ability":
                    continue
                target = ref.get("Identifier", "").casefold()
                if target not in abilities:
                    self.error(key, f"Missing referenced rule {target!r}")
                elif ref.get("Relationship") == "Inherits":
                    visit(target)
            pending.remove(key)
            resolved.add(key)

        for ability_key in abilities:
            visit(ability_key)

    def extraction(self, guilds, miracle_lists, baseline):
        for name, old in baseline.items():
            current = guilds.get(name)
            if current is None:
                self.error(name, "Guild missing after migration")
                continue
            if old.get("MiracleList") != miracle_lists.get(name):
                self.error(name, "Ordinary miracle list differs from pre-refactor snapshot")
            for key in ("Type", "DenominationalMiracle", "Availability", "City Benefits", "30kGift"):
                if old.get(key) != current.get(key):
                    self.error(name, f"Mechanically significant metadata changed for {key}")
            for key, value in old.get("Details", {}).items():
                if current.get("Details", {}).get(key) != value:
                    self.error(name, f"Existing wording changed for {key}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--strict", action="store_true", help="Fail when any source question remains")
    args = parser.parse_args()
    validator = Validator()
    try:
        guilds = validator.dictionary(read(RAW / "people/guilds.json"), "guilds")
        catalogue = read(RAW / "specialisation/abilities.json")
        abilities = {key.casefold(): value for key, value in validator.dictionary(catalogue, "abilities").items()}
        choices = validator.dictionary(read(RAW / "specialisation/choice-sets.json"), "choiceSets")
        choices = {key.casefold() for key in choices}
        miracles = {miracle["name"].casefold() for miracle in read(RAW / "words_from_above/miracles.json")}
        lists = validator.dictionary(read(RAW / "people/miracleLists.json"), "miracleLists")
        baseline = read(BASELINE)["guilds"]
        baseline_refs = set()
        for guild in baseline.values():
            for tier in guild.get("Benefits", {}).values():
                for node, _ in nodes(tier):
                    for component in (node, node.get("Replacement", {}), node.get("Patch", {})):
                        baseline_refs.update(component[field] for field in ("AbilityRef", "DescriptionRef") if field in component)
        for name, record in guilds.items():
            validator.guild(name, record, abilities, choices, miracles, baseline_refs)
        for name, levels in lists.items():
            if name not in guilds:
                validator.error(name, "Miracle list belongs to an unknown guild")
            for level, names in levels.items():
                for miracle in names:
                    if miracle.casefold() not in miracles:
                        validator.warning(name, f"Unmatched ordinary miracle at level {level}: {miracle!r}")
        for alias, target in catalogue.get("aliases", {}).items():
            if not isinstance(target, str) or target.casefold() not in abilities:
                validator.warning(alias, f"Alias does not target a canonical ability: {target!r}")
        validator.inheritance(abilities)
        validator.extraction(guilds, lists, baseline)
    except (KeyError, ValueError, TypeError, AttributeError) as exc:
        validator.error("Data", str(exc))

    for severity, reports in (("ERROR", validator.errors), ("WARNING", validator.warnings)):
        for report in sorted(set(reports)):
            print(f"{severity}: {report}")
    print(f"{len(set(validator.errors))} errors, {len(set(validator.warnings))} outstanding warnings")
    return int(bool(validator.errors or (args.strict and validator.warnings)))


if __name__ == "__main__":
    raise SystemExit(main())
