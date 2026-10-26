using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Application.Listening;
using KorTrainer.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class ListeningServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetExercises_PlaysOnlyWordsTheLearnerHasStarted()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("level1-1");
        var studied = await t.StudyAsync(0, "가다", "바다", "학교", "사람", "시간", "친구", "음식", "가방", "의자", "책");
        var service = new ListeningService(t.Db, t.Cursor, new Random(1));

        var set = await service.GetExercisesAsync(ExerciseTypes.ListenMeaning, 20, Ct);

        set.Module.Should().Be(StudyModule.Listening);
        set.Items.Should().HaveCount(20).And.OnlyContain(i => studied.Select(w => w.Korean).Contains(i.SpeakText!));
    }

    [Fact]
    public async Task GetExercises_BeforeEnoughWords_FillsInWithTheStepsOwnWords()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("lesson-5");
        var opened = await t.Db.Words
            .Where(w => w.Stage == Domain.Plan.PlanStage.FirstPhrases && w.LessonDay <= 5)
            .Select(w => w.Korean)
            .ToListAsync(Ct);
        var service = new ListeningService(t.Db, t.Cursor, new Random(2));

        var set = await service.GetExercisesAsync(ExerciseTypes.ListenSpelling, 10, Ct);

        set.Items.Should().HaveCount(10).And.OnlyContain(i => i.Options.Count == 4 && i.AutoSpeak);
        set.Items.Should().OnlyContain(i => opened.Contains(i.SpeakText!), "words of later lessons are not played yet");
    }

    [Fact]
    public async Task GetExercises_RejectsUnknownType()
    {
        await using var t = await TestDatabase.CreateAsync();
        var service = new ListeningService(t.Db, t.Cursor, new Random(3));

        var act = () => service.GetExercisesAsync("dictation", 5, Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }
}
