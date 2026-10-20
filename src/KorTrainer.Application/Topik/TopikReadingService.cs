using KorTrainer.Application.Common;
using KorTrainer.Application.Plan;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Application.Topik;

/// <param name="Format">Format id (topic, cloze, …).</param>
/// <param name="Questions">TOPIK I question numbers the format corresponds to.</param>
public sealed record TopikFormatInfo(string Format, string Title, string Questions, string Description);

/// <summary>Original practice items in TOPIK I reading formats, matched to the learner's stage.</summary>
public sealed class TopikReadingService(ITopikItemBank bank, PlanCursor cursor, Random random)
{
    public const int MaxCount = 30;

    public static IReadOnlyList<TopikFormatInfo> Formats { get; } =
    [
        new(ExerciseTypes.TopikTopic, "О чём текст", "31–33", "Две фразы — выберите тему: 과일, 가족, 날씨…"),
        new(ExerciseTypes.TopikCloze, "Пропуск", "34–39", "Слово, частица или форма глагола в пропуске"),
        new(ExerciseTypes.TopikNotice, "Объявление", "40–42", "Найдите утверждение, которое не совпадает с объявлением"),
        new(ExerciseTypes.TopikSameMeaning, "То же содержание", "43–45", "Какое утверждение совпадает с текстом"),
        new(ExerciseTypes.TopikMainIdea, "Главная мысль", "46–48", "Что хочет сказать автор"),
    ];

    public async Task<ExerciseSetDto> GetExercisesAsync(string format, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(format == ExerciseTypes.Mixed || Formats.Any(f => f.Format == format), nameof(format), "Неизвестный формат задания TOPIK.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        // Level 2 items wait until the plan reaches level 2; before level 1 the learner still gets level 1 items.
        var stage = (await cursor.GetAsync(cancellationToken)).Step.Stage;
        var maxStage = stage >= PlanStage.Level2 ? PlanStage.TopikPrep : PlanStage.Level1;
        var items = bank.Items
            .Where(i => (format == ExerciseTypes.Mixed || i.Format == format) && i.Stage <= maxStage)
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(ToDto)
            .ToList();

        string title = format == ExerciseTypes.Mixed ? "TOPIK I: чтение вперемешку" : $"TOPIK I: {Formats.Single(f => f.Format == format).Title}";
        return new ExerciseSetDto(StudyModule.Topik, format, title, items);
    }

    private ChoiceExerciseDto ToDto(TopikItem item) => new(
        item.Format,
        $"topik:{item.Id}",
        item.Prompt,
        bank.Instructions.GetValueOrDefault(item.Format),
        null,
        item.Options.Select(o => new ChoiceOptionDto(o, null, null)).ToList(),
        item.Answer,
        $"{item.Explanation} Перевод: {item.Translation}",
        null,
        false);
}
