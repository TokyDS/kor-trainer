using Android.App;
using Android.Content.PM;
using Android.OS;
using KorTrainer.Mobile.Services;

namespace KorTrainer.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        StudyReminders.CreateChannel(this);
        StudyReminders.Schedule(this);
        _ = RequestNotificationPermissionAsync();
    }

    // Android 13+ asks the user once; without it reminders are simply not shown.
    private static async Task RequestNotificationPermissionAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
    }
}
