using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.UI.Services;

/// <summary>Client-wide state: the learner's settings and the speech capabilities of the device.</summary>
public sealed class AppState(IKorTrainerApi api, BrowserInterop browser, ISpeechOutput speech)
{
    private static readonly SettingsDto Fallback = new(
        new DateOnly(2026, 10, 13), UserSettings.DefaultTimeZoneId, false, ThemePreference.System, 10, 200, 0.9);

    private HashSet<string> _recordedWords = [];
    private Task? _initialization;

    public event Action? Changed;

    public SettingsDto Settings { get; private set; } = Fallback;

    public SpeechInfo? Speech { get; private set; }

    public bool IsLoaded { get; private set; }

    public string? LoadError { get; private set; }

    /// <summary>Loads settings, the recorded-word list and speech info once; concurrent callers share the same load.</summary>
    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            Settings = await api.GetSettingsAsync();
            LoadError = null;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            LoadError = ex.Message;
        }

        IsLoaded = true;
        try
        {
            _recordedWords = (await api.GetRecordedWordsAsync()).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            // Without the list every word is simply voiced by speech synthesis.
        }

        try
        {
            await browser.ApplyThemeAsync(Settings.Theme.ToString());
            Speech = await speech.GetSpeechInfoAsync();
        }
        catch (Microsoft.JSInterop.JSException)
        {
            // Theme and speech are enhancements; the trainer works without them.
            Speech = new SpeechInfo(false, false, null, "other");
        }

        Changed?.Invoke();
    }

    public async Task SaveSettingsAsync(SettingsDto settings)
    {
        Settings = await api.SaveSettingsAsync(settings);
        await browser.ApplyThemeAsync(Settings.Theme.ToString());
        Changed?.Invoke();
    }

    /// <summary>Speech rate for listening drills: slower speech keeps consonant contrasts audible.</summary>
    public const double ListeningRate = 0.75;

    /// <summary>The speed the bundled neural speech was generated at (tools/tts/generate_melo.py).</summary>
    private const double SynthesizedSpeed = 0.9;

    /// <summary>
    /// Voices Korean text: a native-speaker recording when one exists (Lingua Libre), otherwise the bundled
    /// neural voice (MeloTTS), otherwise the device's speech synthesis — each step also when the previous
    /// one cannot play right now.
    /// </summary>
    /// <param name="slow">Listening drill: cap the speech rate at <see cref="ListeningRate"/>.</param>
    public async ValueTask SpeakAsync(string? text, bool slow = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string trimmed = text.Trim();
        double rate = slow ? Math.Min(Settings.SpeechRate, ListeningRate) : Settings.SpeechRate;
        if (HasRecording(trimmed) && await TryPlayAsync(() => speech.PlayRecordingAsync(trimmed)))
        {
            return;
        }

        if (await TryPlayAsync(() => speech.PlaySynthesizedAsync(trimmed, Math.Clamp(rate / SynthesizedSpeed, 0.5, 1.5))))
        {
            return;
        }

        await speech.SpeakAsync(trimmed, rate);
    }

    private static async ValueTask<bool> TryPlayAsync(Func<ValueTask<bool>> play)
    {
        try
        {
            return await play();
        }
        catch (Microsoft.JSInterop.JSException)
        {
            return false;
        }
    }

    public bool HasRecording(string? text) => text is not null && _recordedWords.Contains(text.Trim());

    public ValueTask<IReadOnlyList<VoiceInfo>> GetKoreanVoicesAsync() => speech.GetKoreanVoicesAsync();

    public async Task ChooseVoiceAsync(string name)
    {
        await speech.ChooseVoiceAsync(name);
        Speech = await speech.GetSpeechInfoAsync();
        Changed?.Invoke();
    }

    /// <summary>Romanization for display, or null when the learner keeps it hidden.</summary>
    public string? RomanizationFor(string? text, string? precomputed = null)
    {
        if (!Settings.ShowRomanization || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return precomputed ?? (text.Any(Syllable.IsSyllable) ? Romanizer.Romanize(text) : null);
    }
}
