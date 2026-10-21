namespace KorTrainer.UI.Services;

/// <summary>Speech through the browser: Web Speech API voices and recordings served by the API.</summary>
public sealed class BrowserSpeechOutput(BrowserInterop browser) : ISpeechOutput
{
    public ValueTask<SpeechInfo> GetSpeechInfoAsync() => browser.GetSpeechInfoAsync();

    public ValueTask SpeakAsync(string text, double rate) => browser.SpeakAsync(text, rate);

    public ValueTask<bool> PlayRecordingAsync(string word) => browser.PlayRecordingAsync(RecordingUrl(word));

    public ValueTask<bool> PlaySynthesizedAsync(string text, double rate) =>
        browser.PlayRecordingAsync($"api/audio/tts?text={Uri.EscapeDataString(text)}", rate);

    public ValueTask<IReadOnlyList<VoiceInfo>> GetKoreanVoicesAsync() => browser.GetKoreanVoicesAsync();

    public ValueTask<string?> ChooseVoiceAsync(string name) => browser.ChooseVoiceAsync(name);

    public static string RecordingUrl(string word) => $"api/audio/{Uri.EscapeDataString(word)}";
}
