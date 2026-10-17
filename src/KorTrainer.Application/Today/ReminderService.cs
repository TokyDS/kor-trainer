using KorTrainer.Contracts;

namespace KorTrainer.Application.Today;

/// <summary>Decides whether the learner should be nudged today and with what message.</summary>
public sealed class ReminderService(TodayService todayService)
{
    public async Task<ReminderDto> GetAsync(CancellationToken cancellationToken) =>
        Build(await todayService.GetTodayAsync(cancellationToken));

    public static ReminderDto Build(TodayDto today)
    {
        ArgumentNullException.ThrowIfNull(today);
        int done = today.Tasks.Count(t => t.IsDone);
        int total = today.Tasks.Count;
        int due = today.Vocabulary.DueNow;
        bool remind = today.DayNumber >= 1 && (!today.StudiedToday || done < total);

        string title = !today.StudiedToday && today.StreakDays > 0
            ? $"Серия {today.StreakDays} дн. под угрозой"
            : !today.StudiedToday
                ? "Сегодня ещё не было занятий"
                : done < total
                    ? $"Задачи дня: {done} из {total}"
                    : "Все задачи дня выполнены";

        var lines = new List<string>();
        if (done < total)
        {
            lines.Add($"Осталось задач: {total - done}.");
        }

        if (due > 0)
        {
            lines.Add($"Слов к повторению: {due}.");
        }

        if (today.Lesson is { } lesson && done < total)
        {
            lines.Add($"Урок дня {lesson.Day}: {lesson.Title}.");
        }

        return new ReminderDto(remind, title, string.Join(" ", lines), today.StreakDays, today.StudiedToday, total - done, due);
    }
}
