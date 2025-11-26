## setup local: you'll need dotnet 8
`curl -L https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh; chmod +x dotnet-install.sh; ./dotnet-install.sh --channel 8.0`
1. run this to add dotnet to path export PATH="$HOME/.dotnet:$PATH" 
1. run dotnet --info to check above worked 
1. run dotnet workload install maui to install maui workload 
1. run dotnet workload install maui-android to install android workload
`brew install --cask temurin17`

## debug steps:
`PKG=bard.uk.labyitems`
`dotnet build -t:Run -f net8.0-android -c Debug`
`adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1`
`PID=$(adb shell pidof -s "$PKG"); echo "$PID"`
`adb logcat --pid "$PID" -v time`

## view debug logs
`PKG=bard.uk.labyitems`
`dotnet build -t:Run -f net8.0-android -c Debug`

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
   `AndroidSigningKeyPass=<keypass> AndroidSigningStorePass=<storepass> dotnet publish labyItems/labyItems.csproj -f net8.0-android -c Release -p:AndroidSigningKeyAlias=labyItemsSigningKey -p:AndroidPackageFormat=apk -p:GenerateAppBundle=false`
