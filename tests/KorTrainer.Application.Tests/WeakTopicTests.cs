using AwesomeAssertions;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;

namespace KorTrainer.Application.Tests;

public sealed class WeakTopicTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<AttemptDto> Answers(StudyModule module, string type, int right, int wrong) =>
        Enumerable.Range(0, right + wrong).Select(i => new AttemptDto(module, type, $"{type}:{i}", i < right, 1000));

    [Fact]
    public async Task GetWeakTopics_ListsTheWorstTopicsOfTheLastMonth_WithLinks()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([.. Answers(StudyModule.Grammar, ExerciseTypes.Past, 6, 6)], Ct);
        await t.Activity.RecordAttemptsAsync([.. Answers(StudyModule.Numbers, ExerciseTypes.Counters, 8, 4)], Ct);
        await t.Activity.RecordAttemptsAsync([.. Answers(StudyModule.Grammar, ExerciseTypes.PolitePresent, 19, 1)], Ct);

        var weak = await t.Stats.GetWeakTopicsAsync(Ct);

        weak.Should().Equal(
            new WeakTopicDto(StudyModule.Grammar, ExerciseTypes.Past, "Грамматика: -았/었어요", 12, 50, "/grammar/past"),
            new WeakTopicDto(StudyModule.Numbers, ExerciseTypes.Counters, "Числа: Счётные слова", 12, 67, "/numbers/counter"));
    }

    [Fact]
    public async Task GetWeakTopics_ForgetsAnswersOlderThanAMonth()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([.. Answers(StudyModule.Grammar, ExerciseTypes.Past, 0, 12)], Ct);

        t.Time.Advance(TimeSpan.FromDays(WeakTopics.Days + 1));

        (await t.Stats.GetWeakTopicsAsync(Ct)).Should().BeEmpty();
    }
}
