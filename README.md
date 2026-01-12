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
DOTNET_USE_POLLING_FILE_WATCHER=1 $HOME/.dotnet/dotnet watch --project labyItems/labyItems.csproj --framework net10.0-android run
```

If you hit `NETSDK1147` (missing `maui-android`) or similar, you’re probably running the system `dotnet` instead of the one installed by `dotnet-install.sh` — the command above pins to `$HOME/.dotnet/dotnet`.

If watch ever complains about launch profiles, ensure `Properties/launchSettings.json` contains the `Android` profile (added in this repo).

## debug steps:

Single shot debug launch + logcat (add `-clp:ErrorsOnly` to keep noisy XamlC warnings out of the console):

```bash
PKG=bard.uk.labyitems; $HOME/.dotnet/dotnet build -t:Run -f net10.0-android -c Debug -clp:ErrorsOnly && adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 && PID=$(adb shell pidof -s "$PKG" | tr -d '\r'); echo "PID=$PID"; adb logcat --pid "$PID" -v time
```

## view debug logs

`PKG=bard.uk.labyitems`
`$HOME/.dotnet/dotnet build -t:Run -f net10.0-android -c Debug`

`PID=$(adb shell pidof -s "$PKG" | tr -d '\r')`
`echo "PID=$PID"`

# Show ALL levels (Verbose, Debug, Info, etc.)

`adb logcat --pid "$PID" -v time --regex '^\[.*'`

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
