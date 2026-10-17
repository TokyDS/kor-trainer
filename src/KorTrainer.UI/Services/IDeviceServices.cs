namespace KorTrainer.UI.Services;

/// <param name="Platform">android, ios, windows, macos, linux, other — picks the voice installation hint.</param>
public sealed record SpeechInfo(bool Supported, bool HasKoreanVoice, string? VoiceName, string Platform);

/// <param name="Local">Installed on the device (otherwise a network voice that needs internet).</param>
public sealed record VoiceInfo(string Name, bool Local);

/// <summary>
/// Korean audio output: speech synthesis and native-speaker recordings. The browser uses the Web Speech
/// API; Android WebView has none, so the app speaks through the system text-to-speech engine.
/// </summary>
public interface ISpeechOutput
{
    ValueTask<SpeechInfo> GetSpeechInfoAsync();

    ValueTask SpeakAsync(string text, double rate);

    /// <summary>Plays the recording of a word; false if it cannot start (offline, missing file).</summary>
    ValueTask<bool> PlayRecordingAsync(string word);

    ValueTask<IReadOnlyList<VoiceInfo>> GetKoreanVoicesAsync();

    /// <summary>Remembers the preferred voice; returns the voice now in use.</summary>
    ValueTask<string?> ChooseVoiceAsync(string name);
}

/// <summary>Hands the progress backup (JSON) to the learner: a download in the browser, the share sheet in the app.</summary>
public interface IBackupExporter
{
    Task ExportAsync();
}
