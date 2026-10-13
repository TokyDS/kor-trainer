using System.Globalization;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Numbers;

namespace KorTrainer.Domain.Exercises;

/// <summary>Number drills in both directions (digits → words, words → digits) with typical-mistake distractors.</summary>
public sealed class NumberExerciseGenerator(Random random)
{
    public const int OptionCount = 4;

    public static IReadOnlyList<string> Types { get; } =
        [ExerciseTypes.SinoNumbers, ExerciseTypes.NativeNumbers, ExerciseTypes.Counters, ExerciseTypes.Time, ExerciseTypes.Money];

    private const int MaxUniqueAttempts = 8;
    private static readonly int[] RoundMinutes = [0, 5, 10, 15, 20, 30, 40, 45, 50];

    public IReadOnlyList<ChoiceExercise> Generate(IReadOnlyList<string> types, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (types.Count == 0 || types.Any(t => !Types.Contains(t)))
        {
            throw new ArgumentException("Unknown number exercise type.", nameof(types));
        }

        var result = new List<ChoiceExercise>(count);
        var seen = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            var exercise = Next(types.Pick(random));
            for (int attempt = 0; attempt < MaxUniqueAttempts && !seen.Add(exercise.ItemKey); attempt++)
            {
                exercise = Next(types.Pick(random));
            }

            result.Add(exercise);
        }

        return result;
    }

    public ChoiceExercise Sino(long number, bool toWords)
    {
        string words = KoreanNumbers.Sino(number);
        string explanation = $"{Digits(number)} = {SinoBreakdown(number)} → {words}. Перед 십, 백, 천 и 만 единицу (일) не произносят; группы считаются по 만 (10 000).";
        var near = Neighbours(number).ToList();
        return toWords
            ? Build(ExerciseTypes.SinoNumbers, $"sino:{number}", Digits(number), "Как читается сино-корейским числом?", words,
                near.Select(KoreanNumbers.Sino).Prepend(WithExplicitOne(words)), explanation, words)
            : Build(ExerciseTypes.SinoNumbers, $"sino-digits:{number}", words, "Какое это число?", Digits(number),
                near.Select(Digits), explanation, words);
    }

    public ChoiceExercise Native(int number, bool toWords)
    {
        string words = KoreanNumbers.Native(number);
        int tens = number / 10 * 10;
        string breakdown = tens > 0 && number % 10 > 0
            ? $"{tens} + {number % 10} → {KoreanNumbers.Native(tens)} + {KoreanNumbers.Native(number % 10)}"
            : words;
        string explanation = $"{number} по-корейски: {breakdown} = {words}. Корейские числа используют для счёта предметов, возраста и часов.";
        var near = Neighbours(number).Where(n => n is >= 1 and <= KoreanNumbers.MaxNative).Select(n => (int)n).ToList();
        return toWords
            ? Build(ExerciseTypes.NativeNumbers, $"native:{number}", number.ToString(CultureInfo.InvariantCulture), "Как это корейским (исконным) числом?", words,
                near.Select(n => KoreanNumbers.Native(n)).Prepend(KoreanNumbers.Sino(number)), explanation, words)
            : Build(ExerciseTypes.NativeNumbers, $"native-digits:{number}", words, "Какое это число?", number.ToString(CultureInfo.InvariantCulture),
                near.Select(n => n.ToString(CultureInfo.InvariantCulture)), explanation, words);
    }

    public ChoiceExercise Counter(Counter counter, int number)
    {
        string correct = Counters.Phrase(counter, number);
        string prefix = counter.Noun.Length > 0 ? counter.Noun + " " : string.Empty;
        var mistakes = new List<string>();
        if (counter.System == NumberSystem.Native)
        {
            mistakes.Add($"{prefix}{KoreanNumbers.Sino(number)} {counter.Word}");
            mistakes.Add($"{prefix}{KoreanNumbers.Native(number)} {counter.Word}");
        }
        else if (number <= KoreanNumbers.MaxNative)
        {
            mistakes.Add($"{prefix}{KoreanNumbers.Native(number, attributive: true)} {counter.Word}");
        }

        mistakes.AddRange(Neighbours(number)
            .Where(n => n is >= 1 and <= KoreanNumbers.MaxNative)
            .Select(n => Counters.Phrase(counter, (int)n)));

        string system = counter.System == NumberSystem.Native ? "корейскими (исконными)" : "сино-корейскими";
        string attributiveNote = counter.System == NumberSystem.Native && KoreanNumbers.Native(number) != KoreanNumbers.Native(number, true)
            ? $" Перед счётным словом {KoreanNumbers.Native(number)} → {KoreanNumbers.Native(number, true)}."
            : string.Empty;
        string what = counter.NounRussian.Length > 0 ? $"{counter.NounRussian}: {number}" : number.ToString(CultureInfo.InvariantCulture);

        return Build(ExerciseTypes.Counters, $"counter:{counter.Word}:{number}", $"{number} {counter.Word}", $"{counter.Meaning} — {what}", correct,
            mistakes, $"{counter.Word} ({counter.Meaning}) считают {system} числами: {correct}.{attributiveNote}", correct);
    }

    public ChoiceExercise Time(int hour, int minute, bool toWords)
    {
        string words = Counters.Time(hour, minute);
        string digits = $"{hour}:{minute:00}";
        string explanation = $"Часы называют корейскими числами ({KoreanNumbers.Native(hour, true)} 시), минуты — сино-корейскими{(minute > 0 ? $" ({KoreanNumbers.Sino(minute)} 분)" : string.Empty)}: {words}.";
        string sinoHour = minute == 0 ? $"{KoreanNumbers.Sino(hour)} 시" : $"{KoreanNumbers.Sino(hour)} 시 {KoreanNumbers.Sino(minute)} 분";
        string nativeMinute = minute == 0 ? $"{KoreanNumbers.Native(hour)} 시" : $"{KoreanNumbers.Native(hour, true)} 시 {KoreanNumbers.Native(minute)} 분";
        int otherHour = hour % 12 + 1;
        int previousHour = (hour + 10) % 12 + 1;
        return toWords
            ? Build(ExerciseTypes.Time, $"time:{digits}", digits, "Который час? Как это сказать?", words,
                [sinoHour, nativeMinute, Counters.Time(otherHour, minute), Counters.Time(previousHour, minute)], explanation, words)
            : Build(ExerciseTypes.Time, $"time-digits:{digits}", words, "Сколько времени?", digits,
                [$"{otherHour}:{minute:00}", $"{hour}:{(minute + 10) % 60:00}", $"{hour}:{(60 - minute) % 60:00}", $"{previousHour}:{minute:00}"], explanation, words);
    }

    public ChoiceExercise Money(long amount, bool toWords)
    {
        string words = Counters.Money(amount);
        string digits = Won(amount);
        var near = new[] { amount * 10, amount / 10, amount + 1000, Math.Max(100, amount - 500) }
            .Where(a => a is >= 100 and <= 9_999_999 && a != amount)
            .Distinct()
            .Shuffled(random)
            .ToList();
        string explanation = $"{digits} → {words}. Деньги называют сино-корейскими числами; 10 000 — это 만 (без 일), а не «десять тысяч».";
        return toWords
            ? Build(ExerciseTypes.Money, $"money:{amount}", digits, "Сколько это стоит? Как сказать?", words,
                near.Select(Counters.Money).Prepend(WithExplicitOne(KoreanNumbers.Sino(amount)) + " 원"), explanation, words)
            : Build(ExerciseTypes.Money, $"money-digits:{amount}", words, "Сколько денег?", digits,
                near.Select(Won), explanation, words);
    }

    private ChoiceExercise Next(string type)
    {
        bool toWords = random.Next(2) == 0;
        return type switch
        {
            ExerciseTypes.SinoNumbers => Sino(RandomSino(), toWords),
            ExerciseTypes.NativeNumbers => Native(random.Next(3) == 0 ? random.Next(21, 100) : random.Next(1, 21), toWords),
            ExerciseTypes.Counters => RandomCounter(),
            ExerciseTypes.Time => Time(random.Next(1, 13), random.Next(3) == 0 ? random.Next(1, 60) : RoundMinutes.Pick(random), toWords),
            _ => Money(RandomAmount(), toWords),
        };
    }

    private ChoiceExercise RandomCounter()
    {
        var counter = Counters.All.Pick(random);
        int max = Math.Min(counter.Max, random.Next(3) == 0 ? counter.Max : 10);
        return Counter(counter, random.Next(1, max + 1));
    }

    private long RandomSino() => random.Next(10) switch
    {
        < 6 => random.Next(1, 100),
        < 9 => random.Next(100, 10_000),
        _ => random.Next(10_000, 100_000),
    };

    private long RandomAmount() => random.Next(3) switch
    {
        0 => random.Next(1, 100) * 100L,
        1 => random.Next(1, 100) * 1000L,
        _ => random.Next(2, 200) * 500L,
    };

    private ChoiceExercise Build(
        string type,
        string key,
        string prompt,
        string note,
        string correct,
        IEnumerable<string> distractors,
        string explanation,
        string speak)
    {
        // Callers list typical mistakes first; they always make it into the options.
        var options = distractors.Where(d => d != correct).Distinct().Select(d => new ChoiceOption(d));
        var (shuffled, index) = ChoiceBuilder.Shuffle(new ChoiceOption(correct), options, OptionCount, random);
        return new ChoiceExercise(type, key, prompt, note, shuffled, index, explanation, SpeakText: speak);
    }

    private IEnumerable<long> Neighbours(long n) =>
        new[] { n + 1, n - 1, n + 10, n - 10, SwapLastDigits(n), n * 10 }
            .Where(x => x > 0 && x != n && x <= KoreanNumbers.MaxSino)
            .Distinct()
            .Shuffled(random);

    private static long SwapLastDigits(long n)
    {
        if (n < 10)
        {
            return n + 10;
        }

        long ones = n % 10;
        long tens = n / 10 % 10;
        return n - ones - (tens * 10) + (ones * 10) + tens;
    }

    // The classic mistake: saying 일 before 십/백/천/만 (일십오, 일만 원).
    private static string WithExplicitOne(string words) =>
        words.Length > 0 && words[0] is '십' or '백' or '천' or '만' ? "일" + words : words.Replace(" 십", " 일십");

    private static string SinoBreakdown(long number)
    {
        var parts = new List<string>();
        long place = 1;
        for (long rest = number; rest > 0; rest /= 10, place *= 10)
        {
            long digit = rest % 10;
            if (digit > 0)
            {
                parts.Insert(0, (digit * place).ToString(CultureInfo.InvariantCulture));
            }
        }

        return parts.Count > 1 ? string.Join(" + ", parts) : Digits(number);
    }

    private static string Digits(long number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Won(long amount) => amount.ToString("#,0", CultureInfo.InvariantCulture) + "원";
}
