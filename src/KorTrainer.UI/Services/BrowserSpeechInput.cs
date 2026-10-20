using Microsoft.JSInterop;

namespace KorTrainer.UI.Services;

/// <param name="Text">Best transcript, or null when nothing was recognized.</param>
/// <param name="Error">User-facing (Russian) reason when listening failed.</param>
public sealed record SpeechInputResult(string? Text, string? Error);

/// <summary>Speech recognition for shadowing: the browser's SpeechRecognition or Android's recognizer.</summary>
public interface ISpeechInput
{
    ValueTask<bool> IsAvailableAsync();

    /// <summary>Listens for one Korean utterance.</summary>
    ValueTask<SpeechInputResult> ListenAsync();
}

/// <summary>Web Speech API recognition (Chrome, Edge, Safari) in Korean.</summary>
public sealed class BrowserSpeechInput(BrowserInterop browser) : ISpeechInput
{
    public async ValueTask<bool> IsAvailableAsync()
    {
        try
        {
            return await browser.InvokeAsync<bool>("speechRecognitionAvailable");
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async ValueTask<SpeechInputResult> ListenAsync()
    {
        try
        {
            return await browser.InvokeAsync<SpeechInputResult>("recognizeKorean");
        }
        catch (JSException ex)
        {
            return new SpeechInputResult(null, $"Распознавание недоступно: {ex.Message}");
        }
    }
}
