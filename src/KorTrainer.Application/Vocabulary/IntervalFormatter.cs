using System.Globalization;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Application.Vocabulary;

/// <summary>Short Russian labels for SRS answers and intervals ("10 мин", "4 дн", "1,5 мес").</summary>
public static class IntervalFormatter
{
    private const double DaysPerMonth = 30;
    private const double DaysPerYear = 365;
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Format(TimeSpan interval) => interval switch
    {
        { TotalMinutes: < 60 } => $"{Math.Max(1, (int)Math.Round(interval.TotalMinutes))} мин",
        { TotalHours: < 24 } => $"{(int)Math.Round(interval.TotalHours)} ч",
        { TotalDays: < DaysPerMonth } => $"{(int)Math.Round(interval.TotalDays)} дн",
        { TotalDays: < DaysPerYear } => $"{(interval.TotalDays / DaysPerMonth).ToString("0.#", Russian)} мес",
        _ => $"{(interval.TotalDays / DaysPerYear).ToString("0.#", Russian)} г",
    };

    public static string Label(Grade grade) => grade switch
    {
        Grade.Again => "Снова",
        Grade.Hard => "Трудно",
        Grade.Good => "Хорошо",
        Grade.Easy => "Легко",
        _ => grade.ToString(),
    };
}
