## task board

https://trello.com/b/UmbW9Vwl/laby-automation

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
  && dotnet build -t:InstallAndroidDependencies -f net10.0-android \
  && brew install --cask temurin@17
```

If you want the PATH change to persist, add `export PATH="$HOME/.dotnet:$PATH"` to your shell profile.

## hot reload (android)

Make sure an emulator or device is running, then:

```bash
PKG=bard.uk.labyitems
DB=output/laby.db
./tools/migrate-any-data.sh "$DB"
adb push "$DB" /data/local/tmp/laby.db
adb shell run-as "$PKG" sh -c 'cd /data/user/0/'"$PKG"' && mkdir -p files && cp /data/local/tmp/laby.db files/laby.db'
```

then run

```bash
DOTNET_USE_POLLING_FILE_WATCHER=1 \
$HOME/.dotnet/dotnet watch \
  --project labyItems/labyItems.csproj \
  --framework net10.0-android \
  run --configuration Debug
```

IOS boot:

```zsh
xcrun simctl list devices available
xcrun simctl install {answer from above} /Users/brbar/Source/labyItems/labyItems/bin/Debug/net10.0-ios/iossimulator-arm64/labyItems.app
xcrun simctl launch {answer from above} bard.uk.labyitems
```

or

```zsh
dotnet build labyItems/labyItems.csproj -t:Run -f net10.0-ios -p:UseIosWorkload=true -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=:v2:udid=<SIMULATOR_UDID>
```

If you hit `NETSDK1147` (missing `maui-android`) or similar, you’re probably running the system `dotnet` instead of the one installed by `dotnet-install.sh` — the command above pins to `$HOME/.dotnet/dotnet`.

If watch ever complains about launch profiles, ensure `Properties/launchSettings.json` contains the `Android` profile (added in this repo).

## Data generation & migrations

- To generate or migrate any SQLite DB against the latest migrations, run: `./tools/migrate-any-data.sh output/laby.db` (optional second arg: custom seed JSON; defaults to `labyItems/Resources/Raw/druids_way/evocs.json`). The script will create the DB from the seed if it does not exist, then apply FluentMigrator migrations.
- The script uses `$HOME/.dotnet/dotnet` by default; override with `DOTNET=/path/to/dotnet ./tools/migrate-any-data.sh ...` if needed.
- APK builds now keep all `Resources/Raw` JSON assets and the `Template.xlsx` so the app and migrations can load packaged data directly.

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
