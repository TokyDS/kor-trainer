# Android app

`src/KorTrainer.Mobile` is a .NET MAUI app that shows the same Blazor pages as the web client
(`src/KorTrainer.UI`) in a `BlazorWebView`. There is no server: `KorTrainer.Embedded` runs the
application services and SQLite inside the app, so the phone works without the laptop and offline.

| Concern | Web client | Android app |
|---|---|---|
| Back end | `HttpKorTrainerApi` → ASP.NET Core API | `LocalKorTrainerApi` → services in-process |
| Speech | Web Speech API (`BrowserSpeechOutput`) | Android `TextToSpeech` (`AndroidSpeechOutput`) |
| Recordings | `/api/audio/{word}` URL | downloaded to the app cache, played from a blob |
| Backup | download from `/api/progress/export` | share sheet (`ShareBackupExporter`) |
| Reminders | systemd timer + Today banner | alarms at 20:00/22:00 + notification |

## One-time setup

```bash
dotnet workload install maui-android
# Android SDK and Microsoft OpenJDK 21 into the given folders:
dotnet build src/KorTrainer.Mobile -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory=$HOME/Android/Sdk -p:JavaSdkDirectory=$HOME/.local/share/jdk-21 \
  -p:AcceptAndroidSDKLicenses=True
```

Release builds are signed with your own key so that updates install over the previous version (Android
refuses an update signed with a different key — and uninstalling deletes the progress). Create it once
and keep it safe; it is never committed:

```bash
mkdir -p ~/.config/kortrainer && chmod 700 ~/.config/kortrainer
echo "KORTRAINER_KEYSTORE_PASSWORD=<a long random password>" > ~/.config/kortrainer/keystore.env
chmod 600 ~/.config/kortrainer/keystore.env
keytool -genkeypair -keystore ~/.config/kortrainer/kortrainer.keystore -storetype PKCS12 \
  -alias kortrainer -keyalg RSA -keysize 3072 -validity 10000 -dname "CN=KorTrainer"
```

## Build and install

```bash
tools/android/build-apk.sh                 # → artifacts/android/KorTrainer.apk
adb install -r artifacts/android/KorTrainer.apk   # phone connected over USB, or copy the file to it
```

Without `adb`, send the APK to the phone (Telegram "Saved messages", Google Drive, a cable) and open it;
Android asks once to allow installs from that app.

## Moving progress from the web version

In the browser: **Настройки → Резервная копия → Сохранить JSON**. In the app: the same section →
tick the confirmation → choose the file. The app's own backup goes to the share sheet.

The Android project is not part of `KorTrainer.slnx`, so `dotnet build`/`dotnet test` at the root do not
need the Android SDK. Its logic is tested on the desktop through `tests/KorTrainer.Embedded.Tests`.
