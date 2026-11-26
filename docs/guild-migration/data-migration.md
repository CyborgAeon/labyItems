# Guild data migration

## Source hierarchy

The initial inputs were the workspace's version 2 guild proposal, the version 1 shared ability catalogue, the tracked legacy guild catalogue, and the printed rules. The two workspace input snapshots are in [backups](backups/). The original unversioned guild asset is recoverable from Git. Version 2 had 663 ability and description references without a matching ability definition even when matched case-insensitively. Recovery first matched the recorded ability reference or legacy key exactly, then checked printed sources for entries missing from both inputs. The shared catalogue gained 636 entries. Copies recovered from the old JSON remain transcriptions.

The primary printed source for this pass is [The Labyrinthe Almanac, Vol. 32, October 2023](../../labyItems/Resources/Raw/people/Almanac.pdf). Page citations below use the printed page, one less than the corresponding PDF page. The [Collated Almanac](../../labyItems/Resources/Raw/people/Collated.pdf) was available for comparison. The forum paths for Apothecary Society and Without Banners returned the forum login page. There was no independently verified later Order of Mercenaries publication in the material available for this pass.

## Verified scenario changes

| Guild | Source and data changes |
| --- | --- |
| The Brood | Pp. 47–48. Shield Self is part of the Gold branch. One choice at day start controls all three tiers. Advanced Gold widens the existing True Believer grant. Advanced Ivory changes the trigger of its own Mobility grant and retains Mobility as its base ability. |
| Council of Jade | Pp. 99–100. One, three or five selections apply to the same choice. The crane's nested item choice is independent per occurrence, with distinct items. Cranes award temporary item-source benefits. The source explicitly says the Ox grants an innate **Disciple**. That reading is preserved without mapping it to Discipline. |
| Knights Errant | Pp. 104–105. One armour option at Basic and both at Intermediate. The effects are marked as conditional armour for aggregation. The separate Ward innate continues to describe the option selected on activation. Advanced awards a second Ward and Never Act Without Reason. |
| Apothecary Society | Existing transcription and [forum citation](https://www.labyrinthe.co.uk/forum/post/9371/). Drinker and Maker options are defined at membership scope. The stored joining requirements preserve the six-skill-level and six-doses-per-section rules. Six referenced benefit definitions are missing from the available source material. |
| The Order of Mercenaries | Pp. 110–111. Ordinary tier life is +3/1, +6/2 total and +9/3 total. Mercenary Solutions is an Intermediate extension that requires the use of Mercenary Problems. The separate Defence grant gains 30-second immunity at Intermediate and full War Cry at Advanced. The second Mercenaries Unite use is part of the separately purchased Experienced Mercenary status, documented under its published status heading. |
| Church of the Tryst | P. 84. The Dark Bargains references Greater Communication and uses Touch of Death as a comparison. The Shadow of the Imbaiu upgrades target the Basic grant. The ordinary miracle list was extracted intact and the denominational reference remains separate. |
| Without Banners | Existing transcription and [forum citation](https://www.labyrinthe.co.uk/forum/post/8872/). The allowance is two at Basic and three at Intermediate. Wizard presence is required for Fires of the Banner according to the existing Intermediate description. The other options' brackets and the separate Live to Minus cap are unverified. Unknown eligibility is represented explicitly. The incomplete catalogue references remain in the data for later recovery. |
| Sons and Daughters of Sirac | P. 94. A single choice between Harmony and Purpose applies to the matching benefits across tiers. Family Strength progresses to two uses; the Advanced spell-study and mana benefits remain descriptive. |
| Shadow Legion | P. 93. Intermediate adds the Determination-like behaviour to the existing War Cry. The Advanced extra War Cry comes from the Mercenary Soldier choice, alongside the Battle Fury and Medic alternatives. |
| Panama Clan | Pp. 111–112. Advanced Minor Prayer now sets the Intermediate award to two total uses. The signed exception to the Forearm Parry requirement remains an issue for manual verification. |
| Lo'Thi | P. 109. Intermediate Owl's Wisdom adds the full Major Prayer rule and Advanced sets the award to two. Totemic Boon and status-specific choice interactions need further source modelling. |
| Bildteve, Mordred's Rest and Sellaville | Pp. 20, 29 and 34. Missing city/Kallah definitions were recovered from the printed text. Bildteve's Destroy award progresses to two uses with the published target-level limits. |

## Legacy selections and identifiers

The character's positional selections used `<guild>::<tier>::<one-based-group>` keys and one-based option values. The following existing groups have explicit `LegacySelectionKey` and `LegacyOptionIds` mappings. The stored option order is recorded in the runtime asset.

| Guild | Legacy groups | New choice IDs |
| --- | --- | --- |
| The Brood | Basic 1, Intermediate 1, Advanced 1 | `b.choice-2`, `i.choice-2`, `a.choice-3` |
| The White Lotus Society | Basic 1 | `b.choice-3` |
| Church of Dead Heroes | Intermediate 1 | `i.choice-3` |
| The Rings of Talthar | Basic 1, Intermediate 1 | `b.choice-5`, `i.choice-2` |
| 18th Imperial Legion | Basic 1, Advanced 1 | `b.choice-1`, `a.choice-1` |

The Gold and Ivory selections were previously stored independently. Conflicting saved tiers require review under the one-choice rule. New choices without verified legacy counterparts have no positional mapping. Canonical ability keys were retained, with duplicated `Key` fields removed from catalogue objects when equal to the enclosing identifier. Explicit upgrade targets refer to the original grant ID throughout its progression. For example, Shadow Legion’s Intermediate upgrade targets `shadow-legion.basic.war-cry`. When the Advanced choice awards another War Cry, that award retains its own grant ID.

## Outstanding data questions

- Recover the original forum records for Apothecary Society, Without Banners and Church of the Eternal Vampire. The remaining 30 distinct unresolved ability references are in these guilds. Retain their IDs and selections until the definitions can be verified.
- Verify the exact names and forms of three ordinary miracle-list entries. `Summon Spiritual Candles`, `Temp Curse` and `Cure Serious` have no exact name match in the standard miracle catalogue. The extracted lists retain the original names, order and level buckets.
- Verify the target of the pre-existing `Immunity to Incarna Mortis miracle` alias, which currently points to an absent canonical ability.
- Complete the remaining Legacy, LegacyOverwrite and Review upgrade records. A scoped source ID and an explicit operation are required, especially where text describes an additive effect or more than one prior grant could be affected. The validator lists each record.
- Continue checking Brood's Intermediate Gold/Ivory drave benefit and the 18th Imperial Legion's higher tiers against later publications. The printed 2023 edition groups the Brood's Gold benefits as drave innates instead of listing their spells separately, and gives 18th Imperial Legion one tier. Existing proposed transcriptions are retained for comparison.
- Totemic Guardians needs a linked predator/prey category across the minor and medium talent choices. Rings of Talthar needs source review for the selected-path effects, the distinct spell/miracle immunity targets and the source's tier-specific gift progression. Duke's Head needs a shared section vote rather than a lifetime innate count. Earlier typed grants and source text remain recoverable for these cases.
- Guild-status purchases are distinct from ordinary tier upgrades. The Mercenary Way text is retained as published, pending structured purchase support. Verify table, cost, stacking and party-context conditions before enabling automatic effects.

## Offline validation and measurements

Run `python3 tools/guild-data/validate.py` from the repository. It detects invalid and duplicate JSON members, case-insensitive ID collisions, broken references and upgrade targets, invalid choice allowances and legacy mappings, unknown rule fields and operators, ability inheritance cycles, lost guild metadata and changed miracle lists. It prints outstanding source questions as warnings. Run with `--strict` to require resolution of the existing warnings as well.

Run `python3 tools/guild-data/measure.py` to reproduce the comparison. The following snapshot compares the original v2 proposal with the migrated assets. Timings are a local Python 3 JSON parser probe, measured with files already read into byte arrays. The first parse is one in-process sample. The warm value is the median of 50 subsequent parses, with fresh objects on each parse. Peak Python allocations use a separate parse under `tracemalloc`.

| Metric | Before | After |
| --- | ---: | ---: |
| Guild records | 86 | 86 |
| Ordinary lists embedded in guilds | 17 | 0 |
| Ordinary lists in their own asset | 0 | 17 |
| Ability definitions | 1,516 | 2,152 |
| Raw guild data bytes | 326,574 | 328,022 |
| Raw ability data bytes | 608,118 | 737,335 |
| Raw separate miracle-list bytes | 0 | 11,414 |
| Combined raw bytes | 934,692 | 1,076,771 |
| Combined gzip bytes | 171,865 | 210,208 |
| First JSON parse | 5.41 ms | 6.24 ms |
| Warm median JSON parse | 4.82 ms | 5.40 ms |
| Peak allocations during separate parse | 3,285,560 B | 3,872,776 B |

The input catalogue omitted hundreds of referenced definitions. Adding recovered text makes the new catalogue larger and the local parser probe slower. These timings measure raw JSON parsing. App startup, eligibility, modal initialization, benefit resolution, battleboard, details and PDF generation require separate .NET or device measurements.
