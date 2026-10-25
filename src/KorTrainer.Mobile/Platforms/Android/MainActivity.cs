using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
using KorTrainer.Mobile.Services;
using View = Android.Views.View;

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
        // A widget tap opens the short session; a restored activity keeps the page it was on.
        if (savedInstanceState is null)
        {
            App.StartRoute = TodayWidget.RouteOf(Intent);
        }

        base.OnCreate(savedInstanceState);

        // Android 15+ draws apps edge to edge: keep the page clear of the status bar, the navigation bar,
        // display cutouts and the on-screen keyboard.
        if (FindViewById(Android.Resource.Id.Content) is { } content)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsPadding());
        }

        StudyReminders.CreateChannel(this);
        StudyReminders.Schedule(this);
        _ = RequestNotificationPermissionAsync();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (TodayWidget.RouteOf(intent) is { } route && Microsoft.Maui.Controls.Application.Current is App app)
        {
            _ = app.NavigateAsync(route);
        }
    }

    // Leaving the app is when the counts on the widget change, and when a weekly copy has the latest progress.
    protected override void OnPause()
    {
        base.OnPause();
        TodayWidget.RequestUpdate(this);
        if (IPlatformApplication.Current?.Services.GetService<DownloadsAutoBackup>() is { } backup)
        {
            _ = backup.SaveIfDueAsync();
        }
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

    private sealed class SystemBarsPadding : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(View? view, WindowInsetsCompat? insets)
        {
            if (view is null || insets is null)
            {
                return insets;
            }

            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout() | WindowInsetsCompat.Type.Ime());
            if (bars is not null)
            {
                view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }

            return WindowInsetsCompat.Consumed;
        }
    }
}
