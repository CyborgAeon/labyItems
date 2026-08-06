# LabyItems Skills: Storage, Caching, and Creation Flows

## Purpose

Use this note as a low-token map for how data is stored and moved in the app.
It focuses on:

- Database choice and platform rationale
- Startup and migration pipeline
- Caching strategy and invalidation
- Creation flows: character, ISP item, MP item, non-standard content

## Database Solution (What is used and why)

- Primary store: SQLite file named laby.db.
- Main app location: AppDataDirectory/laby.db.
- Dev fallback location: output/laby.db.
- Access libraries:
  - Microsoft.Data.Sqlite for write/update and migration/sync operations.
  - sqlite-net read connections for many content queries.

Why this is used for Android and iOS:

- SQLite is cross-platform and native-friendly for MAUI mobile targets.
- Runtime paths avoid reflection-heavy or provider-fragile startup on mobile.
- Mobile startup uses LightweightMigrator (raw SQL) instead of FluentMigrator runner.
- This design avoids known mobile startup/provider issues and is AOT-safe oriented.

Important naming note:

- LiteDbService is a service name, but it writes to SQLite tables (not LiteDB).

## Startup + Migration Pipeline

Runtime startup sequence:

1. Cache cleanup runs first.
2. DefaultDatabaseInstaller ensures laby.db exists in app data:
   - Reuses existing user DB if present.
   - Copies packaged laby.db on first run if available.
3. DatabaseInitializer applies schema and sync:
   - iOS/Android: LightweightMigrator.ApplyInitialSchema.
   - Other platforms: FluentMigrator runner first, then fallback to LightweightMigrator on failure.
4. Synchronizers update packaged defaults and seed/reference data:
   - PackagedDatabaseSynchronizer
   - EvolutionDataSynchronizer
   - AbilityDefinitionDataSynchronizer
   - CharacterReferenceDataSynchronizer

CLI and tooling migration pipeline:

- tools/migrate-any-data.sh
  - If DB is missing, generates it from seed JSON via tools/evocdbgen.
  - Runs tools/migrator (FluentMigrator runner) against the DB.
- tools/evocdbgen
  - Creates base schema and seed rows from packaged JSON.
- tools/migrator
  - Executes MigrationsLib migrations and updates schema version metadata.

## Caching Strategy

There are three cache layers:

1. In-memory service caches (static process caches)
- Examples: ClassService, PeopleService, EvolutionService, SpellService, MiracleService, DruidEvocationService.
- Pattern: lazy load once, reuse until invalidated.

2. Data-sync cache invalidation points
- PackagedDatabaseSynchronizer invalidates relevant service caches after default-data sync.
- NonStandardContentService invalidates specific domain caches after saves.

3. Filesystem cache hygiene
- CacheMaintenanceService removes stale generated files and packaged temp DB copies.
- Also trims cache directory size when above thresholds.

## Creation Flows

### 1) Create character

Entry:
- Character wizard VM (WizardVm).

Flow:
1. User completes Race/Class -> Skills -> Guilds -> Details -> Review.
2. Save action calls CharacterDraftStore.Save.
3. CharacterDraftStore writes via LiteDbService.UpsertDraft.
4. Draft becomes a wallet character row in wallet_characters.

Result:
- Character is persisted and available for assignment and advancement flows.

### 2) Create ISP item

Entry:
- IspCalculator page.

Flow:
1. BuildDeskSubmissionPayload sets SourceFlow = isp.
2. Payload can go to:
   - RecipientPage for direct save/email, or
   - AdvanceCharacterPage for assignment to an existing character.
3. Save writes wallet item through LiteDbService Update/Insert path.
4. PayloadJson stores source flow, abilities, breakdown, and totals.

Result:
- Wallet item with ISP-focused metadata and breakdown.

### 3) Create MP item

Entry:
- MpCalculatorPageBase and derived MP calculator pages.

Flow:
1. BuildSubmissionPayload creates MpSubmissionPayload.
2. Default SourceFlow is monster-point.
3. Payload routes to RecipientPage or character assignment callback.
4. Save writes wallet item through LiteDbService Update/Insert path.

Result:
- Wallet item with MP cost, ISP total, and serialized detail payload.

### 4) Create non-standard content

Entry:
- NonStandardCreateVm (and related non-standard pages).

Flow:
1. User selects entity type (class, race, ability, spell, miracle, evocation).
2. SaveAsync builds NonStandardSaveRequest and payload JSON.
3. NonStandardContentService.SaveAsync upserts into domain tables.
4. For class/race, life-scale mappings are upserted too.
5. Related caches are invalidated so UI/search sees fresh data.

Result:
- Non-standard data is persisted into existing core tables with nonStandard markers.

## Token-Efficient Lookup Shortcuts

Use these first before broad file reads:

- token-goat read "labyItems/Services/DatabaseInitializer.cs::RunAsync"
- token-goat read "labyItems/Services/DefaultDatabaseInstaller.cs::EnsureDatabaseAsync"
- token-goat read "labyItems/Services/PackagedDatabaseSynchronizer.cs::EnsureCurrentAsync"
- token-goat read "labyItems/Services/CacheMaintenanceService.cs::RunStartupCleanup"
- token-goat read "labyItems/Services/LiteDbService.cs::UpsertDraft"
- token-goat read "labyItems/Pages/Calculator/MpCalculatorPageBase.cs::BuildSubmissionPayload"
- token-goat read "labyItems/Pages/Calculator/IspCalculator.xaml.cs::BuildDeskSubmissionPayload"
- token-goat read "labyItems/Pages/RecipientPage.xaml.cs::SaveSubmissionToWallet"
- token-goat read "labyItems/Pages/NonStandard/NonStandardCreateVm.cs::SaveAsync"
- token-goat read "labyItems/Services/NonStandardContentService.cs::SaveAsync"
- token-goat read "tools/migrate-any-data.sh"
- token-goat read "tools/migrator/Program.cs::Main"

## Key Files

- App DI and startup wiring: labyItems/MauiProgram.cs
- Runtime DB init: labyItems/Services/DatabaseInitializer.cs
- Packaged DB install: labyItems/Services/DefaultDatabaseInstaller.cs
- Mobile-safe schema apply: labyItems/Services/LightweightMigrator.cs
- Wallet persistence: labyItems/Services/LiteDbService.cs
- Non-standard persistence: labyItems/Services/NonStandardContentService.cs
- Migration script wrapper: tools/migrate-any-data.sh
- Migration runner: tools/migrator/Program.cs
- Seed DB generator: tools/evocdbgen/Program.cs
