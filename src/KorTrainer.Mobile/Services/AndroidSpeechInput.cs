using Android.Content;
using Android.OS;
using Android.Speech;
using KorTrainer.UI.Services;

namespace KorTrainer.Mobile.Services;

/// <summary>Korean speech recognition through Android's recognizer (WebView has no SpeechRecognition).</summary>
public sealed class AndroidSpeechInput : ISpeechInput
{
    private const string Korean = "ko-KR";

    public ValueTask<bool> IsAvailableAsync() => ValueTask.FromResult(SpeechRecognizer.IsRecognitionAvailable(Platform.AppContext));

    public async ValueTask<SpeechInputResult> ListenAsync()
    {
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            return new SpeechInputResult(null, "Нет доступа к микрофону — разрешите его в настройках приложения.");
        }

        var result = new TaskCompletionSource<SpeechInputResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        SpeechRecognizer? recognizer = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            recognizer = SpeechRecognizer.CreateSpeechRecognizer(Platform.AppContext);
            if (recognizer is null)
            {
                result.TrySetResult(new SpeechInputResult(null, "Распознавание речи недоступно на этом телефоне."));
                return;
            }

            recognizer.SetRecognitionListener(new Listener(result));
            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, Korean);
            intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, Korean);
            intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
            recognizer.StartListening(intent);
        });

        try
        {
            return await result.Task;
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
            result.TrySetResult(text is { Length: > 0 }
                ? new SpeechInputResult(text, null)
                : new SpeechInputResult(null, "Не услышал речь — попробуйте ещё раз."));
        }

        public void OnError(SpeechRecognizerError error) => result.TrySetResult(new SpeechInputResult(null, error switch
        {
            SpeechRecognizerError.NoMatch or SpeechRecognizerError.SpeechTimeout => "Не услышал речь — попробуйте ещё раз, ближе к микрофону.",
            SpeechRecognizerError.InsufficientPermissions => "Нет доступа к микрофону.",
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
