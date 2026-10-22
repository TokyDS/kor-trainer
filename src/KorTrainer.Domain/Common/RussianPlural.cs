namespace KorTrainer.Domain.Common;

/// <summary>Russian noun forms after a number: 1 слово, 2 слова, 5 слов, 11 слов, 21 слово.</summary>
public static class RussianPlural
{
    public static string Of(int count, string one, string few, string many) => (Math.Abs(count) % 10, Math.Abs(count) % 100) switch
    {
        (_, >= 11 and <= 14) => many,
        (1, _) => one,
        (>= 2 and <= 4, _) => few,
        _ => many,
    };

    /// <summary>The number with its noun: «3 ошибки».</summary>
    public static string Count(int count, string one, string few, string many) => $"{count} {Of(count, one, few, many)}";
}
