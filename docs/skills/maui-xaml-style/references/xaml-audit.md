# XAML styling audit — 2 November 2026

Scope: all 171 application XAML files, including shared controls, resource dictionaries, DTO XAML, and the Windows application entry point. Structural review covers element types, surface styles, spacing, typography, colours, bindings, layouts, and action dimensions. Device rendering and screen-reader behavior are not verified by this audit.

## Implemented

Replaced 109 Frame controls in 31 files with Border, migrated six shared style declarations (one base and five named variants), and renamed named style references. Preserved card padding, colours, shapes, gestures, and content. Converted selected-card elevation to an explicit Shadow, five Frame ancestor bindings to Border, and two wallet C# type checks/lookups. Existing Border surfaces keep their implicit style.

## Conventions and follow-up work

- Shared card radii are 8/12; chip/pill radii are 14/999. Calendar and picker families also deliberately use 16/18 or other sizes. Use the shared style for the matching role rather than normalizing all shapes.
- Libre Caslon Text and shared detail labels provide a consistent base. Some pages still repeat local typography and literal colours. Consolidate within the affected feature when changing it.
- Detail tables require white/#FAF8F3 alternation per the detail-card skill, while the general RowBackgroundEven token is #F8FAFC. These are currently distinct conventions; do not silently substitute one for the other.
- CharacterSpecialisation.xaml and Controls/StepIndicator.xaml retain obsolete AndExpand options. Migrate them with layout-specific checks when those layouts are changed.
- Many templates do not declare their own x:DataType. This is a review candidate, not proof of a broken binding: inherited contexts and intentionally dynamic data need inspection before annotating.
- Compact actions sometimes request dimensions below the global 44-unit minimum, including explicit minimum-size overrides. Verify the effective hit area and enlarge the action container when editing those flows.
- The palette is largely light and several domain states use literal colours. Full theme and contrast coverage remains a coordinated follow-up.

## Per-file inventory

Counts describe the post-migration source. “Untyped” counts DataTemplate elements without a local x:DataType. “Literal colours” counts direct hex colour attributes. “Small dimensions” counts positive action width/height/minimum attributes below 44, not measured hit areas. These counts identify review candidates, not automatic defects.

| File (under labyItems) | Borders | Untyped templates | Literal colours | AndExpand | Small dimensions |
| --- | ---: | ---: | ---: | ---: | ---: |
| `App.xaml` | 0 | 0 | 0 | 0 | 0 |
| `AppShell.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/AdvanceSectionActionRow.xaml` | 1 | 0 | 1 | 0 | 2 |
| `Controls/BackNavigationButton.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/BurgerMenuButton.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/CheckboxControl.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/ChevronIconView.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/ChipGroup/ChipGroup.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/ClassBracketIconView.xaml` | 2 | 0 | 0 | 0 | 0 |
| `Controls/CombatQuickFactsView.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/DetailPageHeader.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/DictionarySlider.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/DictionarySliderHeader.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/ExpandableSectionCard.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Controls/GlyphActionRowView.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/LevelAbilityTableView.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Controls/LevelValueStripView.xaml` | 1 | 1 | 1 | 0 | 0 |
| `Controls/ListControls/RepeatingEntryList.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/ListItemRowView.xaml` | 1 | 0 | 0 | 0 | 8 |
| `Controls/LoadingIndicator.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/MultiChipGroup.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/AlignmentGridPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/AlignmentToggleGridPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/AlignmentsPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/ArmourPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/ElfColourPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/ElfInnateLevelPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/EvocationFieldPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/PowerbasePicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/ResistanceTypePicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/ShieldTypePicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/SpherePicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/UndeadTouchEffectPicker.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Pickers/WeaponBasePicker.xaml` | 3 | 1 | 0 | 0 | 0 |
| `Controls/Pickers/WeaponTypePicker.xaml` | 3 | 1 | 0 | 0 | 0 |
| `Controls/PlusMinusControl.xaml` | 0 | 0 | 0 | 0 | 2 |
| `Controls/SearchFilterBar.xaml` | 1 | 0 | 1 | 0 | 2 |
| `Controls/SearchFilterModal.xaml` | 2 | 2 | 1 | 0 | 0 |
| `Controls/Sliders/VitaeSlider.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Controls/Specialisations/CascadingSpecialisationView.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/Specialisations/SpecialisationChoiceView.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Controls/Specialisations/SpecialisationSectionView.xaml` | 2 | 0 | 0 | 0 | 0 |
| `Controls/SpendOrderRowView.xaml` | 2 | 0 | 0 | 0 | 0 |
| `Controls/StepIndicator.xaml` | 0 | 0 | 0 | 3 | 0 |
| `Controls/StickyFooterControl.xaml` | 2 | 1 | 0 | 0 | 0 |
| `Controls/Switches/TwoOptionSwitch.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Controls/ToolsetNavigationMenu.xaml` | 25 | 0 | 1 | 0 | 0 |
| `ItemFormPage.xaml` | 1 | 1 | 1 | 0 | 0 |
| `Models/DTOs/Miracle.xaml` | 1 | 1 | 0 | 0 | 0 |
| `Pages/AbilityCard/AbilityCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/AbilityCard/AbilityCardComparisonPage.xaml` | 0 | 1 | 0 | 0 | 0 |
| `Pages/AbilityCard/AbilityCardOptionListPage.xaml` | 1 | 0 | 0 | 0 | 2 |
| `Pages/AbilityCard/AbilityDetailCardView.xaml` | 2 | 3 | 1 | 0 | 4 |
| `Pages/Battleboard/BattleboardCharacterListPage.xaml` | 1 | 1 | 0 | 0 | 0 |
| `Pages/Battleboard/BattleboardDamageModalPage.xaml` | 7 | 0 | 0 | 0 | 0 |
| `Pages/Battleboard/BattleboardPage.xaml` | 20 | 7 | 1 | 0 | 0 |
| `Pages/Calculator/Armour/ArmourConfigPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/CalcNav/ArmourNav.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/CalcNav/CharmNav.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/CalcNav/ConsumNav.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/CalcNav/LifeConfigPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/CalcNav/MoreNav.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/Consumables/ConsumableConfigPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/Evocations/Evocation.xaml` | 1 | 1 | 0 | 0 | 0 |
| `Pages/Calculator/Evocations/EvocationConfigPage.xaml` | 0 | 1 | 0 | 0 | 0 |
| `Pages/Calculator/Evocations/EvocationEntryConfigModalPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/General/General.xaml` | 6 | 4 | 1 | 0 | 4 |
| `Pages/Calculator/General/GeneralAbilityImmunityModalPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/General/GeneralConfigPage.xaml` | 0 | 1 | 0 | 0 | 0 |
| `Pages/Calculator/IspCalculator.xaml` | 2 | 2 | 0 | 0 | 4 |
| `Pages/Calculator/IspComponentTypePage.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/Magic/Spell.xaml` | 1 | 1 | 0 | 0 | 0 |
| `Pages/Calculator/Magic/SpellConfigPage.xaml` | 0 | 1 | 0 | 0 | 0 |
| `Pages/Calculator/Magic/SpellEntryConfigModalPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/MpBasicPage.xaml` | 0 | 4 | 0 | 0 | 0 |
| `Pages/Calculator/MpCalculator.xaml` | 3 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/MpCrewPage.xaml` | 0 | 4 | 0 | 0 | 0 |
| `Pages/Calculator/MpThemedayPage.xaml` | 0 | 4 | 0 | 0 | 0 |
| `Pages/Calculator/Neuronics/NeuronicConfigPage.xaml` | 0 | 1 | 0 | 0 | 0 |
| `Pages/Calculator/Neuronics/NeuronicEntryConfigModalPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/Shield/ShieldConfigPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/Spirit/MiracleConfigPage.xaml` | 1 | 2 | 0 | 0 | 2 |
| `Pages/Calculator/Spirit/MiracleEntryConfigModalPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/Spirit/MiracleGuildSearchPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calculator/Weapon/WeaponConfigPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Calendar/AreaMapPage.xaml` | 1 | 0 | 1 | 0 | 0 |
| `Pages/Calendar/CreateEventPage.xaml` | 5 | 0 | 0 | 0 | 0 |
| `Pages/Calendar/EventDetailsPage.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/Calendar/MyBookingsPage.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/Calendar/UnifiedCalendarPage.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/Characters/AddCharacter.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Characters/AdvanceAbilitySearchPage.xaml` | 7 | 3 | 2 | 0 | 2 |
| `Pages/Characters/AdvanceAbilitySpecialisationPage.xaml` | 2 | 0 | 0 | 0 | 0 |
| `Pages/Characters/AdvanceCharacterPage.xaml` | 14 | 14 | 2 | 0 | 28 |
| `Pages/Characters/AdvanceListSearchPage.xaml` | 7 | 5 | 2 | 0 | 2 |
| `Pages/Characters/CharacterBuilder.xaml` | 11 | 2 | 7 | 0 | 4 |
| `Pages/Characters/CharacterInfoView.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Characters/CharacterReviewPage.xaml` | 18 | 11 | 10 | 0 | 6 |
| `Pages/Characters/CharacterReviewView.xaml` | 0 | 1 | 0 | 0 | 0 |
| `Pages/Characters/CharacterSpecialisation.xaml` | 7 | 5 | 0 | 10 | 8 |
| `Pages/Characters/CharacterWalletPage.xaml` | 2 | 2 | 0 | 0 | 0 |
| `Pages/Characters/CharactersPage.xaml` | 1 | 1 | 0 | 0 | 2 |
| `Pages/Characters/ChoiceSetGroupView.xaml` | 2 | 0 | 0 | 0 | 0 |
| `Pages/Characters/GuildBenefitSelectionPage.xaml` | 3 | 4 | 1 | 0 | 0 |
| `Pages/Characters/GuildPickerSearchView.xaml` | 4 | 1 | 1 | 0 | 0 |
| `Pages/Characters/Guilds.xaml` | 7 | 3 | 6 | 0 | 2 |
| `Pages/Characters/GuildsPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/Characters/MultiClassSpecialisationPage.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/Characters/MultiClassWizardPage.xaml` | 4 | 1 | 0 | 0 | 4 |
| `Pages/Characters/MultiRaceSpecialisationPage.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/Characters/MultiRaceWizardPage.xaml` | 4 | 1 | 0 | 0 | 4 |
| `Pages/Characters/Wizard.xaml` | 3 | 1 | 3 | 0 | 0 |
| `Pages/Characters/WizardClassCardView.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/ClassCard/ClassCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/ClassCard/ClassDetailCardView.xaml` | 0 | 5 | 0 | 0 | 0 |
| `Pages/EvocationCard/EvocationCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/EvocationCard/EvocationDetailCardView.xaml` | 2 | 2 | 0 | 0 | 0 |
| `Pages/ForgotPasswordPage.xaml` | 1 | 0 | 0 | 0 | 0 |
| `Pages/GuildCard/GuildCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/GuildCard/GuildDetailCardView.xaml` | 4 | 7 | 3 | 0 | 0 |
| `Pages/ItemCard/ItemDetailCardPage.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/ItemCard/ItemDetailCardView.xaml` | 1 | 2 | 0 | 0 | 0 |
| `Pages/ItemRoutePage.xaml` | 4 | 0 | 2 | 0 | 0 |
| `Pages/ItemTemplates.xaml` | 1 | 1 | 0 | 0 | 0 |
| `Pages/ItemWalletPage.xaml` | 2 | 2 | 0 | 0 | 2 |
| `Pages/LoginPage.xaml` | 2 | 0 | 16 | 0 | 0 |
| `Pages/Makes/MakeAbility.xaml` | 5 | 3 | 2 | 0 | 4 |
| `Pages/Makes/MakeSheetPage.xaml` | 8 | 8 | 1 | 0 | 12 |
| `Pages/MiracleCard/MiracleCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/MiracleCard/MiracleDetailCardView.xaml` | 1 | 0 | 1 | 0 | 0 |
| `Pages/MultiClassCard/MultiClassCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/MultiClassCard/MultiClassDetailCardView.xaml` | 3 | 2 | 0 | 0 | 0 |
| `Pages/MultiRaceCard/MultiRaceCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/MultiRaceCard/MultiRaceDetailCardView.xaml` | 3 | 3 | 0 | 0 | 0 |
| `Pages/NeuronicCard/NeuronicCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/NeuronicCard/NeuronicDetailCardView.xaml` | 1 | 2 | 0 | 0 | 2 |
| `Pages/NonStandard/CharacterAssignmentPage.xaml` | 2 | 1 | 0 | 0 | 0 |
| `Pages/NonStandard/EvocationCombatEditorPage.xaml` | 1 | 2 | 1 | 0 | 0 |
| `Pages/NonStandard/NonStandardAbilityCreatePage.xaml` | 0 | 0 | 1 | 0 | 2 |
| `Pages/NonStandard/NonStandardAbilityLevelEditorPage.xaml` | 0 | 1 | 0 | 0 | 4 |
| `Pages/NonStandard/NonStandardClassCreatePage.xaml` | 24 | 8 | 0 | 0 | 4 |
| `Pages/NonStandard/NonStandardClassSearchPage.xaml` | 4 | 1 | 2 | 0 | 6 |
| `Pages/NonStandard/NonStandardDashboardPage.xaml` | 14 | 0 | 7 | 0 | 0 |
| `Pages/NonStandard/NonStandardDocumentLinksPage.xaml` | 3 | 1 | 0 | 0 | 2 |
| `Pages/NonStandard/NonStandardEvocationCreatePage.xaml` | 5 | 0 | 1 | 0 | 0 |
| `Pages/NonStandard/NonStandardLegacyCreatePage.xaml` | 16 | 8 | 0 | 0 | 38 |
| `Pages/NonStandard/NonStandardMiracleCreatePage.xaml` | 4 | 0 | 1 | 0 | 0 |
| `Pages/NonStandard/NonStandardNeuronicCreatePage.xaml` | 0 | 0 | 1 | 0 | 2 |
| `Pages/NonStandard/NonStandardRaceAbilityRowEditorPage.xaml` | 0 | 0 | 0 | 0 | 2 |
| `Pages/NonStandard/NonStandardSearchPage.xaml` | 1 | 1 | 0 | 0 | 2 |
| `Pages/NonStandard/NonStandardSpellCreatePage.xaml` | 1 | 0 | 1 | 0 | 2 |
| `Pages/NonStandard/NonStandardTagSearchPage.xaml` | 2 | 2 | 0 | 0 | 4 |
| `Pages/NonStandard/NonStandardWalletPage.xaml` | 1 | 1 | 0 | 0 | 0 |
| `Pages/NonStandard/NonStandardWeaponSkillsLevelEditorPage.xaml` | 0 | 0 | 0 | 0 | 2 |
| `Pages/RaceCard/RaceCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/RaceCard/RaceDetailCardView.xaml` | 1 | 4 | 0 | 0 | 0 |
| `Pages/RecipientPage.xaml` | 2 | 1 | 0 | 0 | 0 |
| `Pages/ScrollBuilderPage.xaml` | 1 | 2 | 1 | 0 | 0 |
| `Pages/Search/ClassSearchPage.xaml` | 4 | 3 | 1 | 0 | 2 |
| `Pages/Search/GlobalSearchPage.xaml` | 6 | 4 | 1 | 0 | 0 |
| `Pages/Search/MultiClassSearchPage.xaml` | 2 | 1 | 0 | 0 | 2 |
| `Pages/Search/MultiRaceSearchPage.xaml` | 2 | 1 | 0 | 0 | 2 |
| `Pages/SpecialisationCard/SpecialisationCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/SpecialisationCard/SpecialisationDetailCardView.xaml` | 2 | 3 | 0 | 0 | 4 |
| `Pages/SpellCard/SpellCard.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Pages/SpellCard/SpellDetailCardView.xaml` | 4 | 1 | 4 | 0 | 0 |
| `Pages/Trade/TradeEntryEditModalPage.xaml` | 1 | 1 | 0 | 0 | 0 |
| `Pages/Trade/TradePage.xaml` | 1 | 0 | 2 | 0 | 5 |
| `Platforms/Windows/App.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Resources/Styles/Colors.xaml` | 0 | 0 | 0 | 0 | 0 |
| `Resources/Styles/Styles.xaml` | 0 | 0 | 0 | 0 | 0 |

## Validation

- All 171 XAML files parse as XML; migrated Border attributes and renamed style resources resolve structurally.
- Application XAML/C# search finds no obsolete Frame type references.
- `dotnet build labyItems/labyItems.csproj -f net10.0-android -t:Compile --no-restore` passed (exit 0), with existing dependency vulnerability warnings.
- No packages were installed or added. The skill validator could not run because its Python environment lacks PyYAML; frontmatter and reference paths were checked directly.
- Device rendering, large-text layout, and assistive-technology interaction remain unverified.
