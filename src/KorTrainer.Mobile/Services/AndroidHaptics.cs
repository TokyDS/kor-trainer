using KorTrainer.UI.Services;

namespace KorTrainer.Mobile.Services;

/// <summary>The phone's own haptic tick (the WebView has no vibration API); needs no permission.</summary>
public sealed class AndroidHaptics : IHaptics
{
    public ValueTask BuzzAsync()
    {
        if (HapticFeedback.Default.IsSupported)
        {
            MainThread.BeginInvokeOnMainThread(() => HapticFeedback.Default.Perform(HapticFeedbackType.LongPress));
        }

        return ValueTask.CompletedTask;
    }
}
