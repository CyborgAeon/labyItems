# Consolidated acquisitions follow-up

`multi-classes.json` and `multi-races.json` use schema 2. An acquisition can refer to a canonical definition and omit its name, type or effect when the values agree exactly. The loader fills omitted fields while preserving explicit local values, including empty strings. Counts, Progression, prerequisites, choice references and SystemEffectsByLevel remain acquisition data. The two catalogues still contain all 790 level grants.

| Dataset | Before | After | Level grants |
| --- | ---: | ---: | ---: |
| Multiclasses | 407,116 B | 312,789 B | 494 |
| Multiraces | 151,650 B | 105,370 B | 296 |

The comparison uses the tracked input for the two files. Exact comparisons found 771 eliminated name fields, 776 eliminated type fields, and 733 eliminated effect fields. The six additional effect removals are Awoken Mind. Those refer to the restored Collated Almanac wording on printed p. 81. Awoken Mind's prior summaries remain in Git. The original summary of level one omitted that the reserve pool starts empty.

## Quantities and usage

Set Frequency on level-scaled abilities. Fractional groups round up from the inclusive acquisition level, and quantities can exceed eight. Explicit fixed or Progression counts retain their own semantics. Pass any source-verified extra skill levels to the intended grant before rounding. The generic calculation accepts these contributions, while advancement purchases still need a verified mapping from each purchase to its target acquisition.

Use Progression for source-level steps. Healing Sap unlocks at multirace level four and uses the full multirace level once unlocked. Aligned Elf reads the chosen racial option from the existing elf-colour choice set. Its sixth-level effective racial level of eight applies to its innates. Body of Stone follows the requested first-level acquisition and Frequency 1. The packaged Races book on printed p. 23 lists it under second level while giving one use per level. The first-level interpretation follows the clarification in the brief.

Use acquisition-level `Usage` for interval policies. Art of Battle's later Daring Deed interval replaces its earlier interval within the same source. Psycher adds a separate interval for Neuronic Penetration when the character owns 24 innates, which remain available separately. The interactive board and workbook display the interval and associated source wording.

## Validation and remaining work

Run the acquisition catalogue, quantity and usage fixtures alongside the guild and battleboard suites. [Character-edit performance](character-edit-performance.md) lists the traced reductions in repeat work and the measured isolated lookup. File size is a separate measure.

A complete character-wide immutable snapshot remains outstanding. So do verified purchase-to-grant mappings, generic innate conversion modifiers, migration of further conditional multi-path effects, and a full cross-source stacking audit including cumulative NAC. The earlier guild catalogue still has the outstanding source questions listed in the [guild migration report](data-migration.md). End-to-end mobile edit timings, on-device UI behaviour and the final PDF render require separate verification.
