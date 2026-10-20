namespace KorTrainer.Domain.Hangul;

public sealed record ContrastWord(string Korean, string Russian)
{
    /// <summary>Initial consonant of the first syllable — the sound being contrasted.</summary>
    public char Consonant => Syllable.Decompose(Korean[0]).Initial;
}

/// <summary>
/// Real words that differ only in plain / aspirated / tense initial consonant (불 · 풀 · 뿔).
/// Speech synthesis renders whole words far more naturally than isolated syllables like 가/카/까,
/// so the listening drill uses these sets. Words are ordered plain → aspirated → tense.
/// </summary>
public static class MinimalPairs
{
    public static IReadOnlyList<IReadOnlyList<ContrastWord>> Sets { get; } =
    [
        [new("개다", "складывать"), new("캐다", "выкапывать"), new("깨다", "разбивать; просыпаться")],
        [new("기", "энергия, дух"), new("키", "рост"), new("끼", "талант, задатки")],
        [new("굴", "устрица"), new("꿀", "мёд")],
        [new("가다", "идти"), new("까다", "чистить (кожуру)")],
        [new("공", "мяч"), new("콩", "фасоль")],
        [new("달", "луна"), new("탈", "маска"), new("딸", "дочь")],
        [new("덕", "добродетель"), new("턱", "подбородок"), new("떡", "рисовый хлебец")],
        [new("들", "поле"), new("틀", "рамка"), new("뜰", "двор")],
        [new("불", "огонь"), new("풀", "трава"), new("뿔", "рог")],
        [new("비다", "быть пустым"), new("피다", "цвести"), new("삐다", "вывихнуть")],
        [new("발", "ступня"), new("팔", "рука")],
        [new("방", "комната"), new("빵", "хлеб")],
        [new("자다", "спать"), new("차다", "пинать; холодный"), new("짜다", "солёный")],
        [new("지다", "проигрывать"), new("치다", "бить"), new("찌다", "варить на пару; полнеть")],
        [new("잠", "сон"), new("참", "правда"), new("짬", "свободная минутка")],
        [new("사다", "покупать"), new("싸다", "дешёвый")],
        [new("살", "плоть; год (возраст)"), new("쌀", "рис (зерно)")],
        [new("시", "стихи; час"), new("씨", "семя")],

        // Sets fully covered by native-speaker recordings (Lingua Libre).
        [new("덜다", "уменьшать"), new("털다", "стряхивать"), new("떨다", "дрожать")],
        [new("갈다", "точить; молоть"), new("깔다", "стелить")],
        [new("듣다", "слушать"), new("뜯다", "отрывать")],
        [new("바다", "море"), new("파다", "копать")],
        [new("비", "дождь"), new("피", "кровь")],
        [new("비우다", "опустошать"), new("피우다", "курить; разжигать")],
        [new("튀다", "брызгать; отскакивать"), new("뛰다", "бегать, прыгать")],
        [new("팔다", "продавать"), new("빨다", "стирать")],
        [new("도끼", "топор"), new("토끼", "заяц")],
    ];

    /// <summary>Fewest recorded sets worth switching the drill to native recordings.</summary>
    public const int MinRecordedSets = 5;

    /// <summary>
    /// Sets to drill. With native recordings available, each set keeps only its recorded words (at least
    /// two), so a question never mixes a human voice with synthesis — the voice itself would give the
    /// answer away. Falls back to all sets when too few are recorded.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<ContrastWord>> Playable(IReadOnlySet<string>? recordedWords)
    {
        if (recordedWords is null || recordedWords.Count == 0)
        {
            return Sets;
        }

        var recorded = Sets
            .Select(set => (IReadOnlyList<ContrastWord>)set.Where(w => recordedWords.Contains(w.Korean)).ToList())
            .Where(set => set.Count >= 2)
            .ToList();
        return recorded.Count >= MinRecordedSets ? recorded : Sets;
    }
}
