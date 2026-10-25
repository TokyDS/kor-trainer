using System.Text.Json;

namespace KorTrainer.UI.Services;

/// <summary>
/// How this device behaves in drills, kept in the browser's storage (each phone or browser its own): moving on after a
/// right answer, a buzz on a wrong one, a soft sound on a right one, and the size of Korean text.
/// </summary>
public sealed class DevicePreferences(BrowserInterop browser)
{
    public const double MinKoreanScale = 0.85;
    public const double MaxKoreanScale = 1.45;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private bool _loaded;

    public bool AutoAdvance { get; set; } = true;

    public bool Vibrate { get; set; } = true;

    public bool Sound { get; set; }

    /// <summary>Korean text size relative to the default (1).</summary>
    public double KoreanScale { get; set; } = 1;

    public async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        string? json = await browser.InvokeAsync<string?>("loadPreferences");
        if (json is { Length: > 0 } && JsonSerializer.Deserialize<Stored>(json, Json) is { } stored)
        {
            AutoAdvance = stored.AutoAdvance;
            Vibrate = stored.Vibrate;
            Sound = stored.Sound;
            KoreanScale = Math.Clamp(stored.KoreanScale, MinKoreanScale, MaxKoreanScale);
        }

        await browser.InvokeVoidAsync("applyKoreanScale", KoreanScale);
    }

    public async Task SaveAsync()
    {
        KoreanScale = Math.Clamp(KoreanScale, MinKoreanScale, MaxKoreanScale);
        await browser.InvokeVoidAsync("savePreferences", JsonSerializer.Serialize(new Stored(AutoAdvance, Vibrate, Sound, KoreanScale), Json));
        await browser.InvokeVoidAsync("applyKoreanScale", KoreanScale);
    }

    private sealed record Stored(bool AutoAdvance, bool Vibrate, bool Sound, double KoreanScale);
}

/// <summary>A short buzz for a wrong answer: the browser's vibration, or the phone's haptic feedback.</summary>
public interface IHaptics
{
    ValueTask BuzzAsync();
}

public sealed class BrowserHaptics(BrowserInterop browser) : IHaptics
{
    public ValueTask BuzzAsync() => browser.InvokeVoidAsync("buzz");
}

/// <param name="IsEmbedded">The back end runs inside the app (Android), so the network is never needed.</param>
public sealed record HostInfo(bool IsEmbedded);
