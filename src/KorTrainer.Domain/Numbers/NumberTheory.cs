using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Numbers;

/// <summary>A row of a rule's table or an example: what is written (25, 3 яблока) and how it is said in Korean.</summary>
public sealed record NumberSample(string Written, string Korean, string? Note = null);

/// <summary>What the learner reads before drilling numbers. Korean is computed by <see cref="KoreanNumbers"/> and
/// <see cref="Counters"/>, so the rule always says what the drills check.</summary>
public sealed record NumberTheory(
    string Type,
    string Title,
    string Meaning,
    IReadOnlyList<string> Steps,
    IReadOnlyList<NumberSample> Table,
    IReadOnlyList<NumberSample> Examples,
    string Pitfall);

public static class NumberTheories
{
    public static NumberTheory? Find(string type) => All.FirstOrDefault(t => t.Type == type);

    public static IReadOnlyList<NumberTheory> All { get; } =
    [
        new(ExerciseTypes.SinoNumbers,
            "Сино-корейские числа",
            "Числа китайского происхождения. Ими называют даты, деньги, номера, минуты и этажи — и вообще любое большое число.",
            [
                "Выучите десять цифр: 일 이 삼 사 오 육 칠 팔 구 십.",
                "Десятки строятся как «два-десять»: 20 = 이십, 35 = 삼십오.",
                "Дальше разряды: 백 (100), 천 (1000), 만 (10 000). «Один» перед ними не говорят: просто 십, 백, 천, 만.",
                "Большие числа считают по 만, а не по тысячам: 15 000 = 만 오천.",
                "Месяцы — число + 월, но 6 = 유월 и 10 = 시월.",
            ],
            [.. Enumerable.Range(1, 10).Select(Sino), Sino(100), Sino(1000), Sino(10_000)],
            [
                Sino(11, "10 + 1"),
                Sino(35, "3 × 10 + 5"),
                Sino(99, "9 × 10 + 9"),
                Sino(1_500, "тысяча + пятьсот"),
                Sino(15_000, "10 000 + 5000: считают по 만"),
                Sino(2_026, "год"),
                new("июнь", Counters.Month(6), "исключение: не 육월"),
                new("октябрь", Counters.Month(10), "исключение: не 십월"),
            ],
            "Не говорят «일십» и «일백» — просто 십, 백. Ноль — 영, а в номерах телефонов чаще 공."),

        new(ExerciseTypes.NativeNumbers,
            "Корейские числа",
            "Исконно корейские числа от 1 до 99. Ими считают предметы и людей, называют возраст и часы.",
            [
                "Выучите 1–10: 하나 둘 셋 넷 다섯 여섯 일곱 여덟 아홉 열.",
                "Десятки — отдельные слова: 열 (10), 스물 (20), 서른 (30), 마흔 (40), 쉰 (50) …",
                "Число = десятки + единицы: 15 = 열다섯, 23 = 스물셋.",
                "Перед счётным словом пять чисел сокращаются: 하나 → 한, 둘 → 두, 셋 → 세, 넷 → 네, 스물 → 스무.",
            ],
            [.. Enumerable.Range(1, 10).Select(n => Native(n)), .. Enumerable.Range(2, 8).Select(t => Native(t * 10))],
            [
                Native(11, "열 + 하나"),
                Native(15, "열 + 다섯"),
                Native(23, "스물 + 셋"),
                Native(99, "아흔 + 아홉"),
                new("1 штука", Counters.Phrase(Counter("개"), 1), "하나 → 한 перед счётным словом"),
                new("20 лет", Counters.Phrase(Counter("살"), 20), "스물 → 스무"),
            ],
            "Больше 99 исконных чисел нет: дальше только сино-корейские. И перед счётным словом не «하나 개», а 한 개."),

        new(ExerciseTypes.Counters,
            "Счётные слова",
            "По-корейски предметы считают через счётное слово: 사과 세 개 — «три (штуки) яблока». Оно зависит от того, что считают.",
            [
                "Порядок: существительное → число → счётное слово: 학생 두 명.",
                "Большинство счётных слов идут с корейскими числами в краткой форме: 한 개, 두 명, 세 잔, 네 권.",
                "С сино-корейскими: 층 (этаж), 번 (номер), 분 (минуты), 원 (воны): 삼 층, 오 분.",
                "Возраст — 살 с корейскими числами: 스무 살.",
            ],
            [.. Counters.All.Select(c => new NumberSample(c.Meaning, c.Word, c.System == NumberSystem.Native ? "корейские числа" : "сино-корейские числа"))],
            [
                new("3 яблока", Counters.Phrase(Counter("개"), 3)),
                new("2 студента", Counters.Phrase(Counter("명"), 2)),
                new("1 чашка кофе", Counters.Phrase(Counter("잔"), 1)),
                new("4 книги", Counters.Phrase(Counter("권"), 4)),
                new("3-й этаж", Counters.Phrase(Counter("층"), 3)),
                new("автобус № 15", Counters.Phrase(Counter("번"), 15)),
            ],
            "Число и счётное слово должны быть из одной системы: не «삼 개» и не «세 층»."),

        new(ExerciseTypes.Time,
            "Время",
            "Часы называют корейскими числами, минуты — сино-корейскими.",
            [
                "Часы: корейское число в краткой форме + 시: 한 시, 세 시, 열두 시.",
                "Минуты: сино-корейское число + 분: 십 분, 삼십 분.",
                "Половина часа — 반: 세 시 반.",
            ],
            [.. Enumerable.Range(1, 12).Select(h => new NumberSample($"{h}:00", Counters.Time(h, 0)))],
            [
                new("3:10", Counters.Time(3, 10)),
                new("10:30", Counters.Time(10, 30)),
                new("12:15", Counters.Time(12, 15)),
                new("1:05", Counters.Time(1, 5)),
            ],
            "Не «삼 시» и не «세 분»: часы и минуты считают разными числами."),

        new(ExerciseTypes.Money,
            "Деньги",
            "Цены называют сино-корейскими числами + 원 (вона).",
            [
                "Число сино-корейское, после него 원: 오천 원.",
                "Считайте по 만: 10 000 = 만 원, 50 000 = 오만 원, 100 000 = 십만 원.",
                "«Один» перед 천 и 만 не говорят: 천 원, 만 원.",
            ],
            [.. new long[] { 1_000, 5_000, 10_000, 50_000 }.Select(Money)],
            [Money(3_500), Money(12_000), Money(250_000)],
            "10 000 — это 만, а не «십천»: тысячи по-корейски собирают в 만."),
    ];

    private static NumberSample Sino(int number) => Sino(number, null);

    private static NumberSample Sino(int number, string? note) => new(Grouped(number), KoreanNumbers.Sino(number), note);

    private static NumberSample Native(int number) => Native(number, null);

    private static NumberSample Native(int number, string? note) => new(Grouped(number), KoreanNumbers.Native(number), note);

    private static NumberSample Money(long amount) => new($"{Grouped(amount)} вон", Counters.Money(amount));

    private static Counter Counter(string word) => Counters.All.Single(c => c.Word == word);

    /// <summary>15 000 with a no-break space, as Russian writes thousands (no culture data needed in the browser).</summary>
    private static string Grouped(long number) =>
        number.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '\u00A0');
}
