using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Plan;

/// <summary>The line under «10 минут» on the phone's home-screen widget. Gentle: no streaks, no "behind".</summary>
public static class WidgetSummary
{
    public static string Detail(int dueWords, int dueMistakes, bool studiedToday)
    {
        var parts = new List<string>();
        if (dueWords > 0)
        {
            parts.Add(RussianPlural.Count(dueWords, "слово", "слова", "слов"));
        }

        if (dueMistakes > 0)
        {
            parts.Add(RussianPlural.Count(dueMistakes, "ошибка", "ошибки", "ошибок"));
        }

        if (parts.Count > 0)
        {
            return $"К повторению: {string.Join(" · ", parts)}";
        }

        return studiedToday ? "На сегодня всё повторено" : "Повторять нечего — можно взять новые слова";
    }
}
