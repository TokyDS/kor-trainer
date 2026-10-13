using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Web.Services;

/// <summary>Client-wide state: the learner's settings and the speech capabilities of the browser.</summary>
public sealed class AppState(ApiClient api, BrowserInterop browser)
{
    private static readonly SettingsDto Fallback = new(
        new DateOnly(2026, 10, 13), UserSettings.DefaultTimeZoneId, false, ThemePreference.System, 10, 200, 0.9);

    public event Action? Changed;

    public SettingsDto Settings { get; private set; } = Fallback;

    public SpeechInfo? Speech { get; private set; }

    public bool IsLoaded { get; private set; }

    public string? LoadError { get; private set; }

    public async Task InitializeAsync()
    {
        if (IsLoaded)
        {
            return;
        }

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
            await browser.ApplyThemeAsync(Settings.Theme.ToString());
            Speech = await browser.GetSpeechInfoAsync();
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

    public ValueTask SpeakAsync(string? text) => browser.SpeakAsync(text, Settings.SpeechRate);

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
