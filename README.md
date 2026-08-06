## Required setup

This repo targets .NET 10 and MAUI. The minimum local setup is:

- .NET 10 SDK installed and available on `PATH`.
- MAUI workloads installed: `maui`, `maui-android`, `ios`.
- Java 17 for Android builds (macOS: `brew install --cask temurin@17`).
- Android SDK/NDK available for Android targets, or `dotnet workload install maui-android` will install required SDK bits.
- If you install via the Microsoft install script, add `$HOME/.dotnet` to your PATH or use `$HOME/.dotnet/dotnet` explicitly.

Example bootstrap for macOS or Linux:

```bash
curl -L https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh \
  && chmod +x dotnet-install.sh \
  && ./dotnet-install.sh --channel 10.0 \
  && export PATH="$HOME/.dotnet:$PATH" \
  && dotnet --info \
  && dotnet workload install maui \
  && dotnet workload install maui-android \
  && dotnet workload install ios \
  && dotnet build -t:InstallAndroidDependencies -f net10.0-android
```

If the shell cannot find `dotnet` after install, ensure the `PATH` change is persisted in your profile or use:

```bash
export PATH="$HOME/.dotnet:$PATH"
$HOME/.dotnet/dotnet --info
```

Example bootstrap for Windows (PowerShell):

```powershell
Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
.\dotnet-install.ps1 -Channel 10.0 -Version 10.0.101
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
dotnet nuget add source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json -n dotnet10
dotnet workload install maui maui-android ios
```

## task board

https://trello.com/b/UmbW9Vwl/laby-automation

## Admin Auth & changes API

https://dash.cloudflare.com/0ff838b499061f3416722568f1e57adc/workers/services/view/laby-auth/production

## setup local: you'll need dotnet 10

```bash
# one-shot bootstrap for new dev machines/containers (macOS: brew needed for Java 17)
curl -L https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh \
  && chmod +x dotnet-install.sh \
  && ./dotnet-install.sh --channel 10.0 \
  && export PATH="$HOME/.dotnet:$PATH" \
  && dotnet --info \
  && dotnet workload install maui \
  && dotnet workload install maui-android \
  && dotnet workload install ios \
  && dotnet build labyItems/labyItems.csproj -t:InstallAndroidDependencies -f net10.0-android \
  && brew install --cask temurin@17
```

If you want the PATH change to persist, add `export PATH="$HOME/.dotnet:$PATH"` to your shell profile.

## hot reload (android)

Make sure an Android emulator or device is already running.

### macOS / Linux

Use this when you are running the repo from a bash/zsh shell:

```bash
PKG=bard.uk.labyitems
DB=output/laby.db
./tools/migrate-any-data.sh "$DB"
adb push "$DB" /data/local/tmp/laby.db
adb shell run-as "$PKG" sh -c 'cd /data/user/0/'"$PKG"' && mkdir -p files && cp /data/local/tmp/laby.db files/laby.db'
```

Then start MAUI watch:

```bash
DOTNET_USE_POLLING_FILE_WATCHER=1 \
$HOME/.dotnet/dotnet watch \
  --project labyItems/labyItems.csproj \
  --framework net10.0-android \
  run --configuration Debug
```

If you just ran an iOS-only build and `dotnet watch` reports `NETSDK1005` for `net10.0-android`, refresh restore assets with:

```bash
$HOME/.dotnet/dotnet restore labyItems/labyItems.csproj -p:TargetFramework=net10.0-android
```r

### Windows (PowerShell)

Use this from PowerShell. The bash helper script does not run natively on Windows, so generate and migrate the DB with `dotnet run` directly:

```powershell
$pkg = "bard.uk.labyitems"
$db = "output/laby.db"
$seed = "labyItems/Resources/Raw/druids_way/evocs.json"

New-Item -ItemType Directory -Force -Path output | Out-Null

if (-not (Test-Path $db)) {
  dotnet run --project tools/evocdbgen/evocdbgen.csproj -c Release -- $seed $db
}

dotnet run --project tools/migrator/migrator.csproj -c Release -- $db
adb push $db /data/local/tmp/laby.db
adb shell run-as $pkg sh -c "cd /data/user/0/$pkg && mkdir -p files && cp /data/local/tmp/laby.db files/laby.db"
```

Then start MAUI watch:

```powershell
$env:DOTNET_USE_POLLING_FILE_WATCHER = "1"
dotnet watch --project labyItems/labyItems.csproj --framework net10.0-android run --configuration Debug
```

If `adb` is not found, add your Android SDK `platform-tools` directory to `PATH` first. On a typical Windows install that is:

```powershell
$env:PATH += ";$env:LOCALAPPDATA\Android\Sdk\platform-tools"
```

If `dotnet watch` reports `NETSDK1005` for `net10.0-android`, refresh restore assets with:

```powershell
dotnet restore labyItems/labyItems.csproj -p:TargetFramework=net10.0-android
```

## unit tests (logic-only)

MAUI UI layers are hard to run in fast local unit tests, so this repo includes logic-focused tests (no device/emulator required):

```bash
$HOME/.dotnet/dotnet test tests/labyItems.Tests/labyItems.Tests.csproj
```

## iOS (build latest + push to simulator, keep app data)

**Xcode Version:** This project targets .NET 10 with iOS SDK 26.2.10233, which requires Xcode 26.3 or later. If building with an older version of Xcode, you may need to update your Xcode installation. For CI/CD pipelines, ensure the runner has Xcode 26.3+.

Make sure a simulator is booted, then:

```bash
PKG=bard.uk.labyitems
SIMULATOR_UDID="$(xcrun simctl list devices | awk -F '[()]' '/Booted/{print $2; exit}')"
$HOME/.dotnet/dotnet build labyItems/labyItems.csproj \
  -t:Rebuild \
  -f net10.0-ios \
  -c Debug \
  -p:UseIosWorkload=true \
  -p:RuntimeIdentifier=iossimulator-arm64
APP_PATH="labyItems/bin/Debug/net10.0-ios/iossimulator-arm64/labyItems.app"
xcrun simctl terminate "$SIMULATOR_UDID" "$PKG" || true
xcrun simctl install "$SIMULATOR_UDID" "$APP_PATH"
xcrun simctl launch "$SIMULATOR_UDID" "$PKG"
```

The install command above updates the app in place and preserves simulator app data (including saved character data).
If you explicitly want a clean reset, run:

```bash
xcrun simctl uninstall "$SIMULATOR_UDID" "$PKG"
```

`dotnet build -t:Run` on iOS stays attached to app output/logs and can look like it is "stuck"; use `Ctrl+C` to detach.

then push the latest DB into that simulator app container:
quick refresh db:

```bash
rm -f output/laby.db
./tools/migrate-any-data.sh output/laby.db
```

```bash
PKG=bard.uk.labyitems
DB=output/laby.db
SIMULATOR_UDID="$(xcrun simctl list devices | awk -F '[()]' '/Booted/{print $2; exit}')"
./tools/migrate-any-data.sh "$DB"
APP_DATA_DIR="$(xcrun simctl get_app_container "$SIMULATOR_UDID" "$PKG" data)"
cp "$DB" "$APP_DATA_DIR/Library/laby.db"
chmod 666 "$APP_DATA_DIR/Library/laby.db"
rm -f "$APP_DATA_DIR/Library/laby.db-wal" "$APP_DATA_DIR/Library/laby.db-shm"
xcrun simctl terminate "$SIMULATOR_UDID" "$PKG" || true
xcrun simctl launch "$SIMULATOR_UDID" "$PKG"
```

run as if you're an end IOS user (strict AOT simulator, clean rebuild)

```bash
rm -rf labyItems/bin/Debug/net10.0-ios labyItems/obj/Debug/net10.0-ios
$HOME/.dotnet/dotnet build labyItems/labyItems.csproj \
  -t:Rebuild \
  -f net10.0-ios \
  -c Debug \
  -p:UseIosWorkload=true \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:IosStrictAotSimulator=true \
  -p:IosFailFastFullAotSimulator=false \
  -v minimal
```

get error logs:

```bash
DATA_DIR="$(xcrun simctl get_app_container booted bard.uk.labyitems data)"
find "$DATA_DIR" -name runtime.log -maxdepth 5 -print
cat "$DATA_DIR"/Library/runtime.log
xcrun simctl spawn booted log show --style compact --last 10m --predicate 'process == "labyItems"'
```

If you hit `NETSDK1147` (missing `ios`/`maui-android`) or similar, you’re probably running mixed dotnet installs. This repo uses `$HOME/.dotnet/dotnet`, so install workloads with that exact binary (and do not use `sudo`), e.g.:

```bash
$HOME/.dotnet/dotnet workload install ios maui-android
```

If watch ever complains about launch profiles, ensure `Properties/launchSettings.json` contains the `Android` profile (added in this repo).

## Data generation & migrations

- To generate or migrate any SQLite DB against the latest migrations, run: `./tools/migrate-any-data.sh output/laby.db` (optional second arg: custom seed JSON; defaults to `labyItems/Resources/Raw/druids_way/evocs.json`). The script will create the DB from the seed if it does not exist, then apply FluentMigrator migrations.
- The script uses `$HOME/.dotnet/dotnet` by default; override with `DOTNET=/path/to/dotnet ./tools/migrate-any-data.sh ...` if needed.
- APK builds now keep all `Resources/Raw` JSON assets and the `Template.xlsx` so the app and migrations can load packaged data directly.

## Skills index

- `docs/skills/storage-caching-flows.md` - concise reference for database storage, caching strategy, startup/migrations, and create flows (character, ISP item, MP item, non-standard).
- `docs/skills/token-goat-playbook.md` - token-goat-first command playbook for low-token code lookup in this repo.

## Android release signing (CI)

1. Create a fresh keystore (default alias `labyItemsSigningKey` matches Directory.Build.props):
   `keytool -genkeypair -v -storetype JKS -keystore labyItems/labyItems.keystore -alias labyItemsSigningKey -keyalg RSA -keysize 2048 -validity 10000 -storepass "<storepass>" -keypass "<keypass>" -dname "CN=labyItems, OU=Mobile, O=laby, L=, S=, C="`
2. Base64 the keystore so the workflow can restore it: `base64 -i labyItems/labyItems.keystore -o labyItems.keystore.b64`
3. Add GitHub Actions secrets:
   - `ANDROID_KEYSTORE_BASE64` = contents of `labyItems.keystore.b64`
   - `ANDROID_KEY_ALIAS` = `labyItemsSigningKey` (or your alias)
   - `ANDROID_KEY_PASSWORD` = `<keypass>`
   - `ANDROID_KEYSTORE_PASSWORD` = `<storepass>`
4. For local release builds, place the keystore at `labyItems/labyItems.keystore` and pass passwords/alias when publishing, e.g.:
   `AndroidSigningKeyPass=<keypass> AndroidSigningStorePass=<storepass> $HOME/.dotnet/dotnet publish labyItems/labyItems.csproj -f net10.0-android -c Release -clp:ErrorsOnly -p:AndroidSigningKeyAlias=labyItemsSigningKey -p:AndroidPackageFormat=apk -p:GenerateAppBundle=false`
