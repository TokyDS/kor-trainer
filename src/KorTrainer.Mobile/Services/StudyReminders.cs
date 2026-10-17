using Android.App;
using Android.Content;
using AndroidX.Core.App;
using KorTrainer.UI.Services;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace KorTrainer.Mobile.Services;

/// <summary>
/// Evening reminders like the desktop timer: alarms at 20:00 and 22:00 ask the local back end whether today's
/// study is done and, if not, post a notification that opens the app.
/// </summary>
public sealed class StudyReminders(IKorTrainerApi api, ILogger<StudyReminders> logger)
{
    public const string ChannelId = "study-reminders";
    private const int NotificationId = 20;
    private static readonly int[] ReminderHours = [20, 22];

    /// <summary>(Re)arms the daily alarms; safe to call on every start and after a reboot.</summary>
    public static void Schedule(Context context)
    {
        var alarms = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarms is null)
        {
            return;
        }

        foreach (int hour in ReminderHours)
        {
            var intent = new Intent(context, typeof(ReminderReceiver));
            var pending = PendingIntent.GetBroadcast(context, hour, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            if (pending is null)
            {
                continue;
            }

            // Inexact alarms need no special permission; a few minutes' delay does not matter here.
            alarms.SetInexactRepeating(AlarmType.RtcWakeup, NextOccurrence(hour), AlarmManager.IntervalDay, pending);
        }
    }

    public static void CreateChannel(Context context)
    {
        var channel = new NotificationChannel(ChannelId, "Напоминания о занятиях", NotificationImportance.Default)
        {
            Description = "Вечером, если задачи дня ещё не сделаны.",
        };
        ((NotificationManager?)context.GetSystemService(Context.NotificationService))?.CreateNotificationChannel(channel);
    }

    public async Task CheckAndNotifyAsync(Context context)
    {
        try
        {
            var reminder = await api.GetReminderAsync();
            if (reminder.ShouldRemind)
            {
                Notify(context, reminder.Title, reminder.Message);
            }
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "Could not build the study reminder");
        }
    }

    private static void Notify(Context context, string title, string message)
    {
        var manager = NotificationManagerCompat.From(context);
        if (manager is null || !manager.AreNotificationsEnabled())
        {
            return;
        }

        var open = PendingIntent.GetActivity(
            context, 0, new Intent(context, typeof(MainActivity)).SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetSmallIcon(Resource.Mipmap.appicon_foreground);
        builder.SetContentTitle(title);
        builder.SetContentText(message);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(message));
        builder.SetContentIntent(open);
        builder.SetAutoCancel(true);
        if (builder.Build() is { } notification)
        {
            manager.Notify(NotificationId, notification);
        }
    }

    private static long NextOccurrence(int hour)
    {
        var now = DateTimeOffset.Now;
        var next = new DateTimeOffset(now.Date.AddHours(hour), now.Offset);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next.ToUnixTimeMilliseconds();
    }
}

/// <summary>Fired by the reminder alarms; Android starts the app process (and DI) if it is not running.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || IPlatformApplication.Current?.Services.GetService<StudyReminders>() is not { } reminders)
        {
            return;
        }

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await reminders.CheckAndNotifyAsync(context);
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}

/// <summary>Alarms do not survive a reboot: arm them again.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([Intent.ActionBootCompleted])]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && intent?.Action == Intent.ActionBootCompleted)
        {
            StudyReminders.Schedule(context);
        }
    }
}
