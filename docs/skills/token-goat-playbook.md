# LabyItems Token-Goat Playbook

## Goal

Get exact answers about data/storage and creation flows without reading large files.

## Fast Questions -> Commands

Database and startup:

- What initializes the DB at app launch?
  - token-goat read "labyItems/Services/DatabaseInitializer.cs::InitializeAsync"
  - token-goat read "labyItems/Services/DatabaseInitializer.cs::RunAsync"
- How is first-run DB copied?
  - token-goat read "labyItems/Services/DefaultDatabaseInstaller.cs::EnsureDatabaseAsync"
- Why mobile uses lightweight migrations?
  - token-goat read "labyItems/Services/DatabaseInitializer.cs::RunAsync"
  - token-goat read "labyItems/Services/LightweightMigrator.cs::ApplyInitialSchema"

Caching:

- Which services cache in memory?
  - token-goat read "labyItems/Services/ClassService.cs::GetAllAsync"
  - token-goat read "labyItems/Services/PeopleService.cs::GetAllAsync"
  - token-goat read "labyItems/Services/EvolutionService.cs::GetAllAbilitiesAsync"
  - token-goat read "labyItems/Services/SpellService.cs::GetAllAsync"
- Where are caches invalidated?
  - token-goat read "labyItems/Services/PackagedDatabaseSynchronizer.cs::InvalidateCaches"
  - token-goat read "labyItems/Services/NonStandardContentService.cs::SaveAsync"

Create character:

- How does wizard save?
  - token-goat read "labyItems/Models/ViewModels/WizardVm.cs::SaveToWallet"
  - token-goat read "labyItems/Services/CharacterDraftStore.cs::Save"
  - token-goat read "labyItems/Services/LiteDbService.cs::UpsertDraft"

Create ISP item:

- Where does ISP source flow get set?
  - token-goat read "labyItems/Pages/Calculator/IspCalculator.xaml.cs::BuildDeskSubmissionPayload"
- How does it save to wallet?
  - token-goat read "labyItems/Pages/RecipientPage.xaml.cs::SaveSubmissionToWallet"

Create MP item:

- Where does MP payload come from?
  - token-goat read "labyItems/Pages/Calculator/MpCalculatorPageBase.cs::BuildSubmissionPayload"
- What is default source flow?
  - token-goat read "labyItems/Pages/Calculator/MpCalculatorPageBase.cs::MpSubmissionPayload"

Create non-standard:

- Where is non-standard save logic?
  - token-goat read "labyItems/Pages/NonStandard/NonStandardCreateVm.cs::SaveAsync"
  - token-goat read "labyItems/Services/NonStandardContentService.cs::SaveAsync"

Migrations tooling:

- Full DB migration script:
  - token-goat read "tools/migrate-any-data.sh"
- Migration runner entrypoint:
  - token-goat read "tools/migrator/Program.cs::Main"
- Seed DB generator:
  - token-goat read "tools/evocdbgen/Program.cs::Main"

## Usage Pattern

1. Read only the method you need with token-goat read.
2. If unclear, read one adjacent method only.
3. Only then read a full file.

This keeps token use low and avoids loading unrelated UI code.
