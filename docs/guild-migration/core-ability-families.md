# Core ability families

A family groups published abilities with a recurring mechanic. Family labels support catalogue discovery and review. The stable ability identifier still identifies a specific published rule and the grant identifier identifies the award to a character. A family never powers inheritance or grants eligibility on its own.

The inventory included the packaged people and classes catalogues (29 and 55 top-level entries), multiclasses and multiraces (78 and 31), 91 specialisation choice sets, 1,425 evolution purchases, guilds, and the power catalogues (268 miracle, 520 spell, 128 evocation and 102 neuronic rows). A name review identified the examples below. Existing structured effect and ability types provided the basis for broader armour, life, resistance, casting-level and immunity tags. Entries with generic types such as `Static` and `Innate` were tagged selectively after checking their wording. Some distinct rules deliberately share a family.

| Family | Canonical examples and variations |
| --- | --- |
| Strength | First Grade of Strength, Second Grade of Strength and Burst of Strength share a damage-grade mechanic. Temporary application remains a separate rule. |
| Weapon mastery | First and Second Weapon Mastery, Third Weapon Mastery (Bows), and O- or H-weapon variants keep their grades and weapon eligibility. Weapon choice uses the existing customisation model. |
| Armour | Enhanced Reflexes is DAC. Existing PAC, MAC, SAC and NAC types represent separately applicable armour. A temporary or companion-dependent grant retains the condition. |
| Life | The +6/2 Stamina rules and ordinary life advances preserve separate total-body and location values. Purchasable catalogue identifiers remain stable even where text matches. |
| Live to Minus | Ordinary Live to Minus and guild applications share a threshold mechanic, with caps, critical restrictions, and attendance conditions remaining in their own rules. |
| Resistance | Resistances preserve their resistance type and amount. Ownership of a resistance does not imply immunity. |
| Casting level | Existing mana, spirit and earthpower casting-level definitions remain separate by powerbase. |
| Immunity | Immunity definitions share a family while preserving their targets, powerbase and restrictions. Temporary War Cry immunity is an effect of the innate. |
| War Cry | The class rule gives a five-minute immunity window. A guild grant such as Mercenaries Defence can change it and keeps its own award identity. |
| Discipline | An independently published defence. Grant wording can permit an alternate trigger without importing a new copy of Discipline's effect. |
| Determination | Keeps the published damage cost and stopping effect interaction. |
| Mobility | Mobility and Athletic Training are movement rules with distinct durations, limits and circumstances. |
| Communication | Handbook Communication and Greater Communication live in the miracle catalogue. Expertise Communication and The Dark Bargains reference or modify this family in their own terms. |
| Prayer | Major Prayer is a single Greater Communication question. Mercenary Problems supplies the circumstances for a grant and Mercenary Solutions requires use of that grant. Minor Prayer and similarly named rules need individual comparison before merging. |
| Scout skill | Standard and specialist scout options are projected from the existing choice catalogue. The Swarm’s limited Trap-Wary rule links to the underlying skill. Skill-level progression remains choice-specific. |
| Discern | Discerns share a family while retaining their targets and outputs, for example nature of wounds, undead or spiritual aura. |
| Regeneration | The published Regeneration rule retains its rate and exceptions. |
| Healing | Healing Facility, Self Heal and Sun's Healing share a purpose. Differences in applicable targets and available pools remain attached to each rule. |

Damage reduction, casting stamina, spell and miracle innates, Lucky Idea and True Believer are recurrent review candidates. Numerical reductions and percentage reductions require distinct calculation semantics. Similar names across class, specialisation and guild data are insufficient grounds for consolidation.

## Verified rule chains

- **The Dark Bargains** preserves the grant wording from the October 2023 Almanac, printed page 84. Its reference to Greater Communication is a modification. The reference to Touch of Death is a comparison restricted to level loss by the published text. TBLP and Mantic are terminology.
- **Greater Communication** modifies Communication's answer format. Its cross-catalogue record uses the stable miracle identity produced by the packaged reference catalogue seeder.
- **Mercenary Problems** and **Mercenary Solutions** preserve the wording printed on page 111 of the October 2023 Almanac. Problems refers to the handbook Major Prayer rule, which modifies Greater Communication to supply one question. Solutions requires a prior Problems use and gives further questions on its own terms.
- The runtime Communication, Greater Communication and Touch of Death descriptions retain the existing miracle transcriptions. In particular, the current Touch of Death description includes material for other miracles and undead. It needs an independent catalogue correction checked against the source. The Dark Bargains only links to it for comparison.

## Rule metadata

Use `Family` and `RuleReferences` in a definition as relevant. Each link declares the target catalogue, identifier and relationship. This is the comparison recorded in The Dark Bargains:

```json
{
  "Catalogue": "Miracle",
  "Identifier": "dbe91ebd-13f4-b5c6-b33a-e2a36c4b5287",
  "Relationship": "ComparableTo"
}
```

Existing non-ability catalogues are reused by their seeded identities. The system rule dataset holds aliases and relationship overlays keyed by those identities, so the underlying rule text continues to come from the original catalogue. The resolver prefers exact stable identifiers and reports ambiguous aliases within the declared catalogue. Extend terminology and aliases only when their meaning is verified in context. Legacy `AsPer` entries retain their existing informational presentation and require review before conversion to typed mechanical inheritance.
