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