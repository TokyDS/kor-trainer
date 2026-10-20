using KorTrainer.Application.Settings;
using KorTrainer.Contracts;

namespace KorTrainer.Application.Today;

/// <summary>
/// A gentle evening nudge on days without study — never about losing a streak. Used by the Today banner,
/// the desktop timer and the phone notification; silent when the learner turned reminders off.
/// </summary>
public sealed class ReminderService(TodayService todayService, SettingsService settingsService)
{
    public async Task<ReminderDto> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        return Build(await todayService.GetTodayAsync(cancellationToken), settings.RemindersEnabled);
    }

    public static ReminderDto Build(TodayDto today, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(today);
        int due = today.Vocabulary.DueNow;
        var step = today.Step;

        string title = due > 0 ? "Можно позаниматься 10 минут" : "Немного корейского сегодня?";
        var lines = new List<string>();
        if (due > 0)
        {
            lines.Add($"Слов к повторению: {due}.");
        }

        lines.Add($"Сейчас: {step.StageTitle} · {step.Title}.");
        return new ReminderDto(enabled && !today.StudiedToday, title, string.Join(" ", lines), today.StudiedToday, today.ActiveDaysThisWeek, due);
    }
}
