using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using KorTrainer.Domain.Plan;
using KorTrainer.UI.Services;

namespace KorTrainer.Mobile.Services;

/// <summary>
/// Home-screen widget: what is due today (words and mistakes) under the «10 минут» card; a tap opens the
/// short session. The counts come from the local back end, like the evening reminder.
/// </summary>
[BroadcastReceiver(Label = "KorTrainer: 10 минут", Exported = true)]
[IntentFilter(["android.appwidget.action.APPWIDGET_UPDATE"])]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_today_info")]
public sealed class TodayWidget : AppWidgetProvider
{
    private const string ExtraRoute = "kortrainer.route";
    private const string QuickRoute = "/quick";
    private const string Placeholder = "Повторить слова и упражнения шага";

    /// <summary>The page a widget tap asks for; anything else in the intent is ignored.</summary>
    public static string? RouteOf(Intent? intent) => intent?.GetStringExtra(ExtraRoute) == QuickRoute ? QuickRoute : null;

    /// <summary>Asks placed widgets to refresh, e.g. after a study session.</summary>
    public static void RequestUpdate(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        var ids = manager?.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(TodayWidget))));
        if (ids is not { Length: > 0 })
        {
            return;
        }

        var intent = new Intent(context, typeof(TodayWidget))
            .SetAction(AppWidgetManager.ActionAppwidgetUpdate)
            .PutExtra(AppWidgetManager.ExtraAppwidgetIds, ids);
        context.SendBroadcast(intent);
    }

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is not { Length: > 0 })
        {
            return;
        }

        // The card works at once; the counts follow when the local database answers.
        appWidgetManager.UpdateAppWidget(appWidgetIds, Views(context, Placeholder));
        if (IPlatformApplication.Current?.Services.GetService<IKorTrainerApi>() is not { } api)
        {
            return;
        }

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var today = await api.GetTodayAsync();
                var mistakes = await api.GetMistakeSummaryAsync();
                string detail = WidgetSummary.Detail(today.Vocabulary.DueNow, mistakes.Due, today.StudiedToday);
                appWidgetManager.UpdateAppWidget(appWidgetIds, Views(context, detail));
            }
            catch (ApiException)
            {
                // Keep the plain card; the next update tries again.
            }
            finally
            {
                pending?.Finish();
            }
        });
    }

    private static RemoteViews Views(Context context, string detail)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_today);
        views.SetTextViewText(Resource.Id.widget_detail, detail);

        var open = new Intent(context, typeof(MainActivity))
            .SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop)
            .PutExtra(ExtraRoute, QuickRoute);
        var tap = PendingIntent.GetActivity(context, 1, open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, tap);
        return views;
    }
}
