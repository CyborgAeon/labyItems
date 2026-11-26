# Guild grants and rule references

The app uses three packaged catalogues. `people/guilds.json` holds membership metadata and version 3 grant definitions. `specialisation/abilities.json` holds the version 1 canonical ability catalogue, including rule wording and typed links. `people/miracleLists.json` holds version 1 ordinary miracle access by guild and level. The miracle catalogue remains the source for the miracle definitions.

[GuildCatalogue](../../labyItems/Services/Guilds/GuildCatalogue.cs) loads the packaged guild records once. [GuildsService](../../labyItems/Services/GuildsService.cs) attaches the existing specialisation index and prepares reference-card projections on demand. [GuildGrantResolver](../../labyItems/Services/GuildGrantResolver.cs) evaluates an ordered series of active nodes against an explicit context. [GuildCharacterBenefitResolver](../../labyItems/Services/Guilds/GuildCharacterBenefitResolver.cs) supplies the existing point-based tier progression, character fields and choice-set templates, then materializes effective abilities for character creation.

The builder projects grants into the existing ability drafts and calculates character statistics. The interactive board and the workbook used for PDF export consume those drafts and share the battleboard effect resolver. Saved-character entry points rebuild the draft before displaying or exporting a board. Catalogue caches follow the existing account session where the source includes member data. Resolution results are produced from the current context on each request.

## Add a guild grant

Use the existing guild name as the dictionary key for compatibility with saved memberships. Place lore headings and original text in `Details`. Keep structured eligibility in `Availability.Rules`, using the existing field names and operators. Specify a stable, guild-scoped `Id` for each grant. `AbilityRef` identifies a canonical ability, and overrides carry grant-specific name, count or wording. Shadow Legion's Basic award references the shared War Cry definition.

```json
{
  "Id": "shadow-legion.basic.war-cry",
  "AbilityRef": "ability.war-cry",
  "Count": 1
}
```

Its actual record also includes the guild's verbal and display name. Its Intermediate patch targets that same ID. The Advanced Mercenary Soldier option can award a distinct War Cry, which keeps a separate instance and follows its own selection.

## Add a choice or upgrade

A `Choice` has stable option IDs, an allowance, timing and a bundle of grants per option. Nest another choice under a grant when each selected instance needs its own configuration. Council of Jade uses `b.ways-of-the-families` with allowance one, then explicit patches set the allowance to three and five. Only the Crane option is repeatable. Each occurrence has a separate item choice. Knights Errant uses a `GrantAll` patch at Intermediate.

`SelectionRef` carries a previously selected branch across tiers. The Brood uses the Basic Gold/Ivory choice at every eligible tier. Select `OnJoin` for membership choices, as in Apothecary Society. Adventure and day selections use the current draft session. Section and use timing are represented by the resolver and require a gameplay caller to collect those choices.

Use `Mode: Patch` with `TargetGrantId` to set count, add a count delta, change a typed amount or alter a choice allowance. Use `Replace` for an actual replacement. Keep targeting the original grant ID through subsequent tiers. The Order of Mercenaries' life progression changes the same Basic grant from +3/1 to +6/2 and +9/3. The published separately purchased status benefits remain in the migration report.

For an existing positional choice, record its exact `LegacySelectionKey` and the verified one-based option order in `LegacyOptionIds`. The resolver reports conflicting saved choices or changed option identities for review. Membership values are written with the character's usual save path.

## Add an ability or rule link

Put original rule text, its provenance and its ability family on the canonical definition. Use `RuleReferences` with a catalogue kind, stable identifier and relationship. Reference synonyms through an explicit catalogue alias. The Dark Bargains links to Greater Communication, which inherits Communication. Its Touch of Death link is `ComparableTo`. The ability card exposes related rules and terminology separately from effective grant wording.

When a stable external rule identifier is unavailable or ambiguous, keep the known wording and record the issue in the [migration report](data-migration.md). Use an ordinary `MiracleListAdd` for verified tier or branch access. Record a denomination's miracle reference separately from the ordinary list. Denominational access alone never seeds a learned miracle.

## Verify changes

Run `python3 tools/guild-data/validate.py` and the focused guild, rule-reference and battleboard fixtures. Use `--strict` to require all outstanding source questions to be cleared. Run `python3 tools/guild-data/measure.py` to compare raw catalogue size and parser measurements with the preserved input. The [dependency audit](dependency-audit.md) covers the original entry points and verification limits.
