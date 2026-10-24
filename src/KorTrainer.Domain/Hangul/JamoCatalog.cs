namespace KorTrainer.Domain.Hangul;

public enum JamoKind
{
    BasicVowel,
    IotizedVowel,
    CompoundVowel,
    BasicConsonant,
    AspiratedConsonant,
    TenseConsonant,
}

/// <summary>
/// Hangul learning stages from the study plan: day 1 — basic letters,
/// day 2 — iotized/compound vowels and the three consonant series, day 3 — batchim.
/// </summary>
public enum HangulLevel
{
    Basics = 1,
    Full = 2,
    Batchim = 3,
}

/// <summary>
/// Teaching data for one letter. <see cref="SoundGroup"/> marks letters that sound alike
/// for a beginner (ㅐ/ㅔ), so they never appear together as answer options.
/// </summary>
public sealed record JamoInfo(
    char Jamo,
    JamoKind Kind,
    string Sound,
    string Hint,
    string SoundGroup,
    string? Name = null)
{
    public bool IsVowel => Kind is JamoKind.BasicVowel or JamoKind.IotizedVowel or JamoKind.CompoundVowel;

    public HangulLevel Level => Kind is JamoKind.BasicVowel or JamoKind.BasicConsonant
        ? HangulLevel.Basics
        : HangulLevel.Full;

    /// <summary>Syllable used to voice the letter: vowels after silent ㅇ (아), consonants before ㅏ (가).</summary>
    public string SampleSyllable => IsVowel
        ? new Syllable('ㅇ', Jamo).ToString()
        : new Syllable(Jamo, 'ㅏ').ToString();

    public string Romanization => IsVowel ? Romanizer.Medial(Jamo) : Romanizer.Initial(Jamo) is { Length: > 0 } r ? r : "-";
}

/// <summary>The 40 letters of modern Hangul with Russian sound approximations.</summary>
public static class JamoCatalog
{
    public static IReadOnlyList<JamoInfo> All { get; } =
    [
        new('ㅏ', JamoKind.BasicVowel, "а", "Как русское «а»: 아버지.", "a"),
        new('ㅓ', JamoKind.BasicVowel, "о (открытое)", "Открытое «о», рот приоткрыт, губы не округлены: 어머니.", "eo"),
        new('ㅗ', JamoKind.BasicVowel, "о (губы трубочкой)", "Закрытое «о», губы округлены и вытянуты: 오이.", "o"),
        new('ㅜ', JamoKind.BasicVowel, "у", "Как русское «у»: 우유.", "u"),
        new('ㅡ', JamoKind.BasicVowel, "ы (губы растянуты)", "Звук между «ы» и «у», губы растянуты в улыбку: 그.", "eu"),
        new('ㅣ', JamoKind.BasicVowel, "и", "Как русское «и»: 이.", "i"),
        new('ㅐ', JamoKind.BasicVowel, "э (рот шире)", "«э», рот открыт шире; в речи почти не отличается от ㅔ: 개.", "e"),
        new('ㅔ', JamoKind.BasicVowel, "э", "Как русское «э»; почти совпадает с ㅐ: 네.", "e"),
        new('ㅑ', JamoKind.IotizedVowel, "я", "ㅏ с черточкой-«й»: 야구.", "ya"),
        new('ㅕ', JamoKind.IotizedVowel, "йо (открытое)", "«й» + открытое ㅓ: 여기.", "yeo"),
        new('ㅛ', JamoKind.IotizedVowel, "йо (губы трубочкой)", "«й» + закрытое ㅗ: 요리.", "yo"),
        new('ㅠ', JamoKind.IotizedVowel, "ю", "«й» + ㅜ: 우유.", "yu"),
        new('ㅒ', JamoKind.IotizedVowel, "йэ (рот шире)", "«й» + ㅐ, встречается редко: 얘기.", "ye"),
        new('ㅖ', JamoKind.IotizedVowel, "йэ", "«й» + ㅔ: 시계 (после согласных часто звучит как ㅔ).", "ye"),
        new('ㅘ', JamoKind.CompoundVowel, "уа", "ㅗ + ㅏ, быстро слитно: 과일.", "wa"),
        new('ㅙ', JamoKind.CompoundVowel, "уэ (ㅗ+ㅐ)", "ㅗ + ㅐ: 왜.", "we"),
        new('ㅚ', JamoKind.CompoundVowel, "уэ (ㅗ+ㅣ)", "Пишется ㅗ + ㅣ, а звучит как «уэ»: 회사.", "we"),
        new('ㅝ', JamoKind.CompoundVowel, "уо", "ㅜ + ㅓ: 뭐.", "wo"),
        new('ㅞ', JamoKind.CompoundVowel, "уэ (ㅜ+ㅔ)", "ㅜ + ㅔ, редкая: 웨이터.", "we"),
        new('ㅟ', JamoKind.CompoundVowel, "уи", "ㅜ + ㅣ: 귀.", "wi"),
        new('ㅢ', JamoKind.CompoundVowel, "ыи", "ㅡ + ㅣ; после согласной звучит как ㅣ: 의사.", "ui"),

        new('ㄱ', JamoKind.BasicConsonant, "к/г", "В начале слова ближе к «к», между гласными — «г»: 가방.", "k", "기역"),
        new('ㄴ', JamoKind.BasicConsonant, "н", "Как русское «н»: 나무.", "n", "니은"),
        new('ㄷ', JamoKind.BasicConsonant, "т/д", "В начале слова ближе к «т», между гласными — «д»: 다리.", "t", "디귿"),
        new('ㄹ', JamoKind.BasicConsonant, "р/л", "Между гласными — «р», в конце слога — «л»: 라면, 말.", "r", "리을"),
        new('ㅁ', JamoKind.BasicConsonant, "м", "Как русское «м»: 머리.", "m", "미음"),
        new('ㅂ', JamoKind.BasicConsonant, "п/б", "В начале слова ближе к «п», между гласными — «б»: 바다.", "p", "비읍"),
        new('ㅅ', JamoKind.BasicConsonant, "с", "«с»; перед ㅣ звучит мягко, почти «щ»: 사람, 시간.", "s", "시옷"),
        new('ㅇ', JamoKind.BasicConsonant, "не читается (в начале)", "В начале слога — пустое место, в конце — носовое «нъ»: 아이, 강.", "ng", "이응"),
        new('ㅈ', JamoKind.BasicConsonant, "ч/дж", "В начале слова ближе к «ч», между гласными — «дж»: 자다.", "j", "지읒"),
        new('ㅎ', JamoKind.BasicConsonant, "х", "Лёгкое «х», как выдох: 하나.", "h", "히읗"),
        new('ㅋ', JamoKind.AspiratedConsonant, "кх (с придыханием)", "«к» с сильным выдохом: 커피.", "kh", "키읔"),
        new('ㅌ', JamoKind.AspiratedConsonant, "тх (с придыханием)", "«т» с сильным выдохом: 토요일.", "th", "티읕"),
        new('ㅍ', JamoKind.AspiratedConsonant, "пх (с придыханием)", "«п» с сильным выдохом: 포도.", "ph", "피읖"),
        new('ㅊ', JamoKind.AspiratedConsonant, "чх (с придыханием)", "«ч» с сильным выдохом: 친구.", "chh", "치읓"),
        new('ㄲ', JamoKind.TenseConsonant, "кк (напряжённое)", "Твёрдое «к» без выдоха, связки напряжены: 까치.", "kk", "쌍기역"),
        new('ㄸ', JamoKind.TenseConsonant, "тт (напряжённое)", "Твёрдое «т» без выдоха: 딸기.", "tt", "쌍디귿"),
        new('ㅃ', JamoKind.TenseConsonant, "пп (напряжённое)", "Твёрдое «п» без выдоха: 빵.", "pp", "쌍비읍"),
        new('ㅆ', JamoKind.TenseConsonant, "сс (напряжённое)", "Твёрдое «с»: 싸다.", "ss", "쌍시옷"),
        new('ㅉ', JamoKind.TenseConsonant, "чч (напряжённое)", "Твёрдое «ч» без выдоха: 짜다.", "jj", "쌍지읒"),
    ];

    /// <summary>The three consonant series drilled by ear: plain, aspirated, tense.</summary>
    public static IReadOnlyList<IReadOnlyList<char>> ConsonantSeries { get; } =
    [
        ['ㄱ', 'ㅋ', 'ㄲ'],
        ['ㄷ', 'ㅌ', 'ㄸ'],
        ['ㅂ', 'ㅍ', 'ㅃ'],
        ['ㅈ', 'ㅊ', 'ㅉ'],
        ['ㅅ', 'ㅆ'],
    ];

    /// <summary>Vowels the series are heard with: open enough that the consonant stands out.</summary>
    public static IReadOnlyList<char> SeriesVowels { get; } = ['ㅏ', 'ㅓ', 'ㅗ', 'ㅜ', 'ㅣ'];

    /// <summary>The row a plain, aspirated or tense consonant belongs to (ㅌ → ㄷ ㅌ ㄸ).</summary>
    public static IReadOnlyList<char> SeriesRow(char consonant) =>
        ConsonantSeries.FirstOrDefault(row => row.Contains(consonant))
        ?? throw new ArgumentException($"'{consonant}' has no plain/aspirated/tense series.", nameof(consonant));

    public static JamoInfo Get(char jamo) =>
        All.FirstOrDefault(j => j.Jamo == jamo)
        ?? throw new ArgumentOutOfRangeException(nameof(jamo), jamo, "Unknown jamo.");

    /// <summary>Letters that are taught up to and including the given level.</summary>
    public static IReadOnlyList<JamoInfo> ForLevel(HangulLevel level) =>
        All.Where(j => j.Level <= level).ToList();

    public static string SeriesName(JamoKind kind) => kind switch
    {
        JamoKind.BasicConsonant => "обычная",
        JamoKind.AspiratedConsonant => "с придыханием",
        JamoKind.TenseConsonant => "напряжённая",
        _ => string.Empty,
    };
}
