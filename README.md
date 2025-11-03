debug steps:
PKG=bard.uk.labyitems
dotnet build -t:Run -f net8.0-android -c Debug
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1
PID=$(adb shell pidof -s "$PKG"); echo "$PID"
adb logcat --pid "$PID" -v time