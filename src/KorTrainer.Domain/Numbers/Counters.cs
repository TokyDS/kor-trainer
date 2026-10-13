namespace KorTrainer.Domain.Numbers;

public enum NumberSystem
{
    Native,
    Sino,
}

/// <param name="Noun">A typical noun counted with this word (Korean, Russian).</param>
public sealed record Counter(string Word, NumberSystem System, string Meaning, string Noun, string NounRussian, int Max);

/// <summary>Counters a beginner meets first and the number system each one takes.</summary>
public static class Counters
{
    public static IReadOnlyList<Counter> All { get; } =
    [
        new("개", NumberSystem.Native, "штуки", "사과", "яблоко", 20),
        new("명", NumberSystem.Native, "люди", "학생", "студент", 30),
        new("살", NumberSystem.Native, "возраст", string.Empty, string.Empty, 99),
        new("권", NumberSystem.Native, "книги", "책", "книга", 12),
        new("잔", NumberSystem.Native, "чашки, стаканы", "커피", "кофе", 10),
        new("마리", NumberSystem.Native, "животные", "고양이", "кошка", 10),
        new("병", NumberSystem.Native, "бутылки", "물", "вода", 10),
        new("층", NumberSystem.Sino, "этаж", string.Empty, string.Empty, 30),
        new("번", NumberSystem.Sino, "номер (автобуса, по порядку)", "버스", "автобус", 99),
        new("분", NumberSystem.Sino, "минуты", string.Empty, string.Empty, 59),
        new("원", NumberSystem.Sino, "воны", string.Empty, string.Empty, 99),
    ];

    /// <summary>"사과 세 개", "삼 층": the number in the counter's system, attributive for native numbers.</summary>
    public static string Phrase(Counter counter, int number)
    {
        ArgumentNullException.ThrowIfNull(counter);
        string numeral = counter.System == NumberSystem.Native
            ? KoreanNumbers.Native(number, attributive: true)
            : KoreanNumbers.Sino(number);
        string counted = $"{numeral} {counter.Word}";
        return counter.Noun.Length > 0 ? $"{counter.Noun} {counted}" : counted;
    }

    /// <summary>Months are Sino-Korean with two exceptions: 6월 = 유월, 10월 = 시월.</summary>
    public static string Month(int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        return month switch
        {
            6 => "유월",
            10 => "시월",
            _ => KoreanNumbers.Sino(month) + "월",
        };
    }

    /// <summary>Clock time: hours in native numbers (세 시), minutes in Sino-Korean (삼십 분).</summary>
    public static string Time(int hour, int minute)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hour, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hour, 12);
        ArgumentOutOfRangeException.ThrowIfNegative(minute);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minute, 59);

        string hours = $"{KoreanNumbers.Native(hour, attributive: true)} 시";
        return minute == 0 ? hours : $"{hours} {KoreanNumbers.Sino(minute)} 분";
    }

    public static string Money(long amount) => $"{KoreanNumbers.Sino(amount)} 원";
}
