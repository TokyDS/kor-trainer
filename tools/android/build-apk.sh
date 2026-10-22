#!/usr/bin/env bash
# Builds the signed release APK of the Android app into artifacts/android/.
# The keystore and its password stay outside the repository (see tools/android/README.md).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
config_dir="${XDG_CONFIG_HOME:-$HOME/.config}/kortrainer"
keystore="${KORTRAINER_KEYSTORE:-$config_dir/kortrainer.keystore}"
keystore_env="${KORTRAINER_KEYSTORE_ENV:-$config_dir/keystore.env}"
output="$repo_root/artifacts/android"

if [[ ! -f "$keystore" || ! -f "$keystore_env" ]]; then
    echo "No signing key: create $keystore and $keystore_env first (see tools/android/README.md)." >&2
    exit 1
fi

set -a
# shellcheck source=/dev/null
source "$keystore_env"
set +a

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
if [[ -z "${JAVA_HOME:-}" && -d "$HOME/.local/share/jdk-21" ]]; then
    export JAVA_HOME="$HOME/.local/share/jdk-21"
fi

# Bundled speech: words and syllables as generated, phrases re-encoded smaller (see the script).
python3 "$repo_root/tools/android/pack_speech.py"

dotnet publish "$repo_root/src/KorTrainer.Mobile/KorTrainer.Mobile.csproj" \
    -f net10.0-android -c Release \
    -p:KorTrainerKeystore="$keystore" \
    -p:AndroidSdkDirectory="$ANDROID_HOME" \
    -p:JavaSdkDirectory="${JAVA_HOME:-}" \
    -o "$output"

apk="$(find "$output" -name '*-Signed.apk' | head -n 1)"
cp "$apk" "$output/KorTrainer.apk"
echo "APK: $output/KorTrainer.apk"
