using Android.Content;
using Android.OS;
using Android.Speech;
using KorTrainer.UI.Services;

namespace KorTrainer.Mobile.Services;

/// <summary>Korean speech recognition through Android's recognizer (WebView has no SpeechRecognition).</summary>
public sealed class AndroidSpeechInput : ISpeechInput
{
    private const string Korean = "ko-KR";
    private const string NoSpeech = "Не услышал речь — попробуйте ещё раз, ближе к микрофону.";

    /// <summary>The recognizer stops by itself after silence; this only guards against a hung service.</summary>
    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(15);

    public ValueTask<bool> IsAvailableAsync() => ValueTask.FromResult(SpeechRecognizer.IsRecognitionAvailable(Platform.AppContext));

    public async ValueTask<SpeechInputResult> ListenAsync()
    {
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            return new SpeechInputResult(null, "Нет доступа к микрофону — разрешите его в настройках приложения.");
        }

        var result = new TaskCompletionSource<SpeechInputResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        SpeechRecognizer? recognizer = null;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                recognizer = SpeechRecognizer.CreateSpeechRecognizer(Platform.AppContext)
                    ?? throw new InvalidOperationException("Speech recognizer is not available.");
                recognizer.SetRecognitionListener(new Listener(result));
                var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraLanguage, Korean);
                intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, Korean);
                intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
                recognizer.StartListening(intent);
            });

            return await result.Task.WaitAsync(ListenTimeout);
        }
        catch (TimeoutException)
        {
            return new SpeechInputResult(null, NoSpeech);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Java.Lang.Exception)
        {
            return new SpeechInputResult(null, "Распознавание речи недоступно на этом телефоне.");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                recognizer?.Destroy();
                recognizer?.Dispose();
            });
        }
    }

    private sealed class Listener(TaskCompletionSource<SpeechInputResult> result) : Java.Lang.Object, IRecognitionListener
    {
        public void OnResults(Bundle? results)
        {
            string? text = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition)?.FirstOrDefault();
            result.TrySetResult(text is { Length: > 0 } ? new SpeechInputResult(text, null) : new SpeechInputResult(null, NoSpeech));
        }

        public void OnError(SpeechRecognizerError error) => result.TrySetResult(new SpeechInputResult(null, error switch
        {
            SpeechRecognizerError.NoMatch or SpeechRecognizerError.SpeechTimeout => NoSpeech,
            // The app has the microphone, but the system recognizer (usually «Синтезатор речи Google») does not.
            SpeechRecognizerError.InsufficientPermissions =>
                "У службы распознавания речи нет доступа к микрофону. Откройте Настройки → Приложения → "
                + "«Синтезатор речи Google» (или «Распознавание и синтез речи Google») → Разрешения → Микрофон → Разрешить.",
            SpeechRecognizerError.Network or SpeechRecognizerError.NetworkTimeout => "Для распознавания нужен интернет или офлайн-пакет корейского языка.",
            _ => $"Ошибка распознавания ({error}).",
        }));

        public void OnBeginningOfSpeech()
        {
        }

        public void OnBufferReceived(byte[]? buffer)
        {
        }

        public void OnEndOfSpeech()
        {
        }

        public void OnEvent(int eventType, Bundle? @params)
        {
        }

        public void OnPartialResults(Bundle? partialResults)
        {
        }

        public void OnReadyForSpeech(Bundle? @params)
        {
        }

        public void OnRmsChanged(float rmsdB)
        {
        }
    }
}
