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

    /// <summary>
    /// Plays pre-generated neural speech of the text at <paramref name="rate"/> times its generated speed;
    /// false when there is none for this text.
    /// </summary>
    ValueTask<bool> PlaySynthesizedAsync(string text, double rate);

    ValueTask<IReadOnlyList<VoiceInfo>> GetKoreanVoicesAsync();

    /// <summary>Remembers the preferred voice; returns the voice now in use.</summary>
    ValueTask<string?> ChooseVoiceAsync(string name);
}

/// <summary>Hands the progress backup (JSON) to the learner: a download in the browser, the share sheet in the app.</summary>
public interface IBackupExporter
{
    Task ExportAsync();
}

/// <param name="LastSavedUtc">When the last automatic copy was written; null before the first one.</param>
/// <param name="Folder">Where the copies are, as a file manager shows it.</param>
public sealed record AutoBackupStatus(DateTime? LastSavedUtc, string Folder);

/// <summary>Weekly copies of the progress into a folder outside the app, so a lost or reset app loses nothing.</summary>
public interface IAutoBackup
{
    /// <summary>Null where no automatic copies are made (the browser keeps data on the server).</summary>
    Task<AutoBackupStatus?> GetStatusAsync();

    /// <summary>Writes a copy now; throws <see cref="IOException"/> when the folder cannot be written.</summary>
    Task<AutoBackupStatus> SaveNowAsync();
}

/// <summary>The browser: progress lives on the server, its backup is the manual export.</summary>
public sealed class NoAutoBackup : IAutoBackup
{
    public Task<AutoBackupStatus?> GetStatusAsync() => Task.FromResult<AutoBackupStatus?>(null);

    public Task<AutoBackupStatus> SaveNowAsync() => throw new NotSupportedException("Автокопии делает только приложение на телефоне.");
}
