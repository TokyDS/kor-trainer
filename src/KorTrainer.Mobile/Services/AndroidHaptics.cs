using KorTrainer.UI.Services;
using Microsoft.Extensions.Logging;

namespace KorTrainer.Mobile.Services;

/// <summary>
/// The phone's own haptic tick (the WebView has no vibration API). MAUI checks that the VIBRATE permission is
/// declared in the manifest. The buzz is a nicety: if it fails, the answer must still count, so nothing is thrown.
/// </summary>
public sealed class AndroidHaptics(ILogger<AndroidHaptics> logger) : IHaptics
{
    public ValueTask BuzzAsync()
    {
        // On the main thread (where the Blazor pages run in the app) this runs at once, inside the caller.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (HapticFeedback.Default.IsSupported)
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                }
            }
            catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException or Java.Lang.Exception)
            {
                logger.LogWarning(ex, "Haptic feedback failed");
            }
        });

        return ValueTask.CompletedTask;
    }
}
