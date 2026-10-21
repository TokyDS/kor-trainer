using Android.Speech.Tts;
using KorTrainer.Application.Common;
using KorTrainer.UI.Services;
using Microsoft.JSInterop;
using Locale = Java.Util.Locale;
using TextToSpeech = Android.Speech.Tts.TextToSpeech;

namespace KorTrainer.Mobile.Services;

/// <summary>
/// Korean speech through Android's text-to-speech engine (WebView has no Web Speech API) and recordings
/// from the local audio library, played by the page's audio element.
/// </summary>
public sealed class AndroidSpeechOutput(BrowserInterop browser, IAudioLibrary audio, ISynthesizedSpeech synthesized) : ISpeechOutput, IDisposable
{
    private const string VoicePreferenceKey = "korean-voice";
    private static readonly TimeSpan EngineStartTimeout = TimeSpan.FromSeconds(5);
    private static readonly Locale Korean = Locale.Korean!;

    private TextToSpeech? _engine;
    private Task<bool>? _ready;

    public async ValueTask<SpeechInfo> GetSpeechInfoAsync()
    {
        if (!await EnsureReadyAsync())
        {
            return new SpeechInfo(false, false, null, "android-app");
        }

        bool hasKorean = SelectKoreanVoice();
        return new SpeechInfo(true, hasKorean, hasKorean ? _engine!.Voice?.Name : null, "android-app");
    }

    public async ValueTask SpeakAsync(string text, double rate)
    {
        if (!await EnsureReadyAsync())
        {
            return;
        }

        await StopRecordingAsync();
        SelectKoreanVoice();
        _engine!.SetSpeechRate((float)rate);
        _engine.Speak(text, QueueMode.Flush, null, Guid.NewGuid().ToString("N"));
    }

    public async ValueTask<bool> PlayRecordingAsync(string word)
    {
        var file = await audio.OpenAsync(word, CancellationToken.None);
        if (file is null)
        {
            return false;
        }

        await using (file.Content)
        {
            _engine?.Stop();
            return await browser.PlayRecordingDataAsync(file.Content, file.ContentType);
        }
    }

    public async ValueTask<bool> PlaySynthesizedAsync(string text, double rate)
    {
        var file = await synthesized.OpenAsync(text, CancellationToken.None);
        if (file is null)
        {
            return false;
        }

        await using (file.Content)
        {
            _engine?.Stop();
            return await browser.PlayRecordingDataAsync(file.Content, file.ContentType, rate);
        }
    }

    public async ValueTask<IReadOnlyList<VoiceInfo>> GetKoreanVoicesAsync()
    {
        if (!await EnsureReadyAsync())
        {
            return [];
        }

        return [.. KoreanVoices()
            .OrderBy(v => v.IsNetworkConnectionRequired)
            .ThenBy(v => v.Name, StringComparer.Ordinal)
            .Select(v => new VoiceInfo(v.Name ?? string.Empty, !v.IsNetworkConnectionRequired))];
    }

    public async ValueTask<string?> ChooseVoiceAsync(string name)
    {
        Preferences.Default.Set(VoicePreferenceKey, name);
        return await EnsureReadyAsync() && SelectKoreanVoice() ? _engine!.Voice?.Name : null;
    }

    public void Dispose()
    {
        _engine?.Stop();
        _engine?.Shutdown();
        _engine?.Dispose();
    }

    private Task<bool> EnsureReadyAsync()
    {
        if (_ready is null)
        {
            var initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _engine = new TextToSpeech(Platform.AppContext, new InitListener(status => initialized.TrySetResult(status == OperationResult.Success)));
            _ready = WithTimeoutAsync(initialized.Task);
        }

        return _ready;
    }

    // Some phones never answer when no speech engine is installed: give up instead of waiting forever.
    private static async Task<bool> WithTimeoutAsync(Task<bool> initialized) =>
        await Task.WhenAny(initialized, Task.Delay(EngineStartTimeout)) == initialized && await initialized;

    /// <summary>Switches the engine to Korean (the learner's voice if still installed); false without Korean data.</summary>
    private bool SelectKoreanVoice()
    {
        var engine = _engine!;
        if (engine.IsLanguageAvailable(Korean) < LanguageAvailableResult.Available)
        {
            return false;
        }

        engine.SetLanguage(Korean);
        string? preferred = Preferences.Default.Get<string?>(VoicePreferenceKey, null);
        var voice = KoreanVoices().FirstOrDefault(v => v.Name == preferred);
        if (voice is not null)
        {
            engine.SetVoice(voice);
        }

        return true;
    }

    private IEnumerable<Voice> KoreanVoices() =>
        _engine?.Voices?.Where(v => v.Locale?.Language == Korean.Language && !v.Features!.Contains(TextToSpeech.Engine.KeyFeatureNotInstalled)) ?? [];

    private async ValueTask StopRecordingAsync()
    {
        try
        {
            await browser.StopAudioAsync();
        }
        catch (JSException)
        {
            // Nothing was playing or the page is reloading.
        }
    }

    private sealed class InitListener(Action<OperationResult> onInit) : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        public void OnInit(OperationResult status) => onInit(status);
    }
}
