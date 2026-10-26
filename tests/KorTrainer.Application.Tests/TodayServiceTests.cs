using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Application.Tests;

public sealed class TodayServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewLearner_StartsAtLessonOne()
    {
        await using var t = await TestDatabase.CreateAsync();

        var today = await t.Today.GetTodayAsync(Ct);

        today.Step.Key.Should().Be("lesson-1");
        today.Step.State.Should().Be(StepState.Current);
        today.Step.Stage.Should().Be(PlanStage.Hangul);
        today.Step.Lesson!.Day.Should().Be(1);
        today.Step.Tasks.Should().NotBeEmpty().And.OnlyContain(task => !task.IsDone);
        today.Step.PreviousKey.Should().BeNull();
        today.Step.NextKey.Should().Be("lesson-2");
        today.ActiveDaysThisWeek.Should().Be(0);
        today.ReadyForNext.Should().BeFalse();
    }

    [Fact]
    public async Task Answers_CountTowardsTheMatchingTaskOnly()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Activity.RecordAttemptsAsync(LetterAnswers(20), Ct);
        var today = await t.Today.GetTodayAsync(Ct);

        var letterTask = today.Step.Tasks.Single(x => x.Key == ExerciseTypes.LetterToSound);
        letterTask.Progress.Should().Be(20);
        letterTask.IsDone.Should().BeTrue();
        today.Step.Tasks.Single(x => x.Key == ExerciseTypes.SoundToLetter).Progress.Should().Be(0);
        today.StudiedToday.Should().BeTrue();
        today.ActiveDaysThisWeek.Should().Be(1);
    }

    [Fact]
    public async Task Progress_AccumulatesAcrossDays_AndTheStepNeverMovesByItself()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Today.GetTodayAsync(Ct);

        await t.Activity.RecordAttemptsAsync(LetterAnswers(10), Ct);
        t.Time.Advance(TimeSpan.FromDays(5));
        await t.Activity.RecordAttemptsAsync(LetterAnswers(10), Ct);
        var today = await t.Today.GetTodayAsync(Ct);

        today.Step.Key.Should().Be("lesson-1");
        today.Step.Tasks.Single(x => x.Key == ExerciseTypes.LetterToSound).IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task AllTasksDone_OffersTheNextStep()
    {
        await using var t = await TestDatabase.CreateAsync();

        await DoLessonOneAsync(t);

        var today = await t.Today.GetTodayAsync(Ct);
        today.ReadyForNext.Should().BeTrue();
        today.Step.NextTitle.Should().Contain("Урок 2");
    }

    [Fact]
    public async Task LessonTasks_OpenOneAfterAnother()
    {
        await using var t = await TestDatabase.CreateAsync();

        var fresh = (await t.Today.GetTodayAsync(Ct)).Step.Tasks;
        await t.Today.MarkReadAsync(new MarkReadRequest(StudyMaterials.HangulGuide(1)), Ct);
        var afterGuide = (await t.Today.GetTodayAsync(Ct)).Step.Tasks;

        fresh.Select(x => x.IsLocked).Should().Equal(false, true, true, true, true, true);
        afterGuide.Select(x => x.IsLocked).Should().Equal(false, false, true, true, true, true);
    }

    [Fact]
    public async Task LessonTask_PractisedOutOfOrder_StaysOpen()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("lesson-5");

        await t.Activity.RecordAttemptsAsync(
            [new AttemptDto(StudyModule.Grammar, ExerciseTypes.Copula, "copula:책", true, 1500)], Ct);
        var tasks = (await t.Today.GetTodayAsync(Ct)).Step.Tasks;

        tasks.Single(x => x.Key == ExerciseTypes.Copula).IsLocked.Should().BeFalse();
        tasks.Single(x => x.Key == "rule").IsLocked.Should().BeTrue("the new words come first");
        tasks[0].Key.Should().Be("vocab-new");
        tasks[0].IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task BlockTasks_AreNeverLocked()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("level1-1");

        (await t.Today.GetTodayAsync(Ct)).Step.Tasks.Should().OnlyContain(x => !x.IsLocked);
    }

    [Fact]
    public async Task MarkRead_CompletesTheRuleOfTheCurrentLesson()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("lesson-5");

        await t.Today.MarkReadAsync(new MarkReadRequest(StudyMaterials.GrammarRule(ExerciseTypes.Copula)), Ct);
        var rule = (await t.Today.GetTodayAsync(Ct)).Step.Tasks.Single(x => x.Key == "rule");

        rule.IsDone.Should().BeTrue();
        rule.IsReading.Should().BeTrue();
    }

    [Fact]
    public async Task MarkRead_OfSomethingTheStepDoesNotAsk_ChangesNothing()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Today.MarkReadAsync(new MarkReadRequest(StudyMaterials.GrammarRule(ExerciseTypes.Copula)), Ct);
        await t.Today.MarkReadAsync(new MarkReadRequest(StudyMaterials.HangulGuide(2)), Ct);

        (await t.Today.GetTodayAsync(Ct)).Step.Tasks.Should().OnlyContain(x => !x.IsDone);
    }

    [Fact]
    public async Task MarkRead_WithoutAMaterial_IsRejected()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Today.MarkReadAsync(new MarkReadRequest(" "), Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Advance_OpensTheNextStep_AndCountsOnlyNewActivity()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Today.GetTodayAsync(Ct);
        await t.Activity.RecordAttemptsAsync(LetterAnswers(20), Ct);

        t.Time.Advance(TimeSpan.FromMinutes(1));
        var next = await t.Today.AdvanceAsync(new AdvanceRequest("lesson-1"), Ct);
        await t.Activity.RecordAttemptsAsync(LetterAnswers(5), Ct);
        var current = await t.Today.GetTodayAsync(Ct);
        var previous = await t.Today.GetStepAsync("lesson-1", Ct);

        next.Step.Key.Should().Be("lesson-2");
        current.Step.Tasks.Single(x => x.Key == ExerciseTypes.LetterToSound).Progress.Should().Be(5);
        previous.Step.State.Should().Be(StepState.Done);
        previous.Step.Tasks.Single(x => x.Key == ExerciseTypes.LetterToSound).Progress.Should().Be(20);
        previous.Step.CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Advance_WorksWithUnfinishedTasks()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("lesson-10");

        var next = await t.Today.AdvanceAsync(new AdvanceRequest("lesson-10"), Ct);

        next.Step.Key.Should().Be("level1-1");
        next.Step.Title.Should().Be("Блок 1 из 16");
    }

    [Fact]
    public async Task Advance_FromAStaleStep_IsAConflict()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Today.AdvanceAsync(new AdvanceRequest("lesson-1"), Ct);

        var act = () => t.Today.AdvanceAsync(new AdvanceRequest("lesson-1"), Ct);

        await act.Should().ThrowAsync<ConflictException>();
        (await t.Today.GetTodayAsync(Ct)).Step.Key.Should().Be("lesson-2");
    }

    [Fact]
    public async Task Advance_PastTheLastStep_IsRejected()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("maintenance");

        var act = () => t.Today.AdvanceAsync(new AdvanceRequest("maintenance"), Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task UpcomingStep_IsAReadOnlyPreview()
    {
        await using var t = await TestDatabase.CreateAsync();

        var preview = await t.Today.GetStepAsync("level1-2", Ct);

        preview.Step.State.Should().Be(StepState.Upcoming);
        preview.IsCurrent.Should().BeFalse();
        preview.CurrentKey.Should().Be("lesson-1");
        preview.Step.Tasks.Should().OnlyContain(task => task.Progress == 0);
    }

    [Fact]
    public async Task UnknownStep_IsNotFound()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => t.Today.GetStepAsync("day-42", Ct)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Blocks_MeasureTheWholeDeck()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("level1-1");
        await IntroduceWordsAsync(t, 3);

        var words = (await t.Today.GetTodayAsync(Ct)).Step.Tasks.Single(x => x.Key == "words");

        words.Progress.Should().Be(3);
        words.Target.Should().Be(Roadmap.WordsPerBlock);
    }

    [Fact]
    public async Task ActiveDays_CountTheLastWeekWithoutStreakPressure()
    {
        await using var t = await TestDatabase.CreateAsync();
        foreach (int gap in new[] { 0, 2, 3 })
        {
            t.Time.Advance(TimeSpan.FromDays(gap));
            await t.Activity.RecordAttemptsAsync(LetterAnswers(1), Ct);
        }

        t.Time.Advance(TimeSpan.FromDays(2));
        var today = await t.Today.GetTodayAsync(Ct);

        today.ActiveDaysThisWeek.Should().Be(2, "the first study day is now a week back and drops out quietly");
        today.StudiedToday.Should().BeFalse();
    }

    [Fact]
    public async Task CalendarLearner_KeepsLessonsDoneOnTheirDay()
    {
        // Lesson 1 fully done on day 1, lesson 2 only started on day 2, opened on day 4.
        await using var t = await TestDatabase.CreateAsync();
        t.Db.StepTaskCompletions.Add(new StepTaskCompletion { StepKey = "lesson-1", TaskKey = "guide", CompletedUtc = t.Clock.UtcNow });
        await t.Db.SaveChangesAsync(Ct);
        await t.Activity.RecordAttemptsAsync(
            [.. LetterAnswers(20), .. Answers(ExerciseTypes.SoundToLetter, 20), .. Answers(ExerciseTypes.Compose, 15), .. Answers(ExerciseTypes.Decompose, 15)], Ct);
        for (int i = 0; i < 10; i++)
        {
            await t.Activity.RecordReadingTrialAsync(new ReadingTrialDto("가나", 2, 1500, true), Ct);
        }

        t.Time.Advance(TimeSpan.FromDays(1));
        await t.Activity.RecordAttemptsAsync(LetterAnswers(7), Ct);
        t.Time.Advance(TimeSpan.FromDays(2));

        var today = await t.Today.GetTodayAsync(Ct);
        var lessonOne = await t.Today.GetStepAsync("lesson-1", Ct);

        today.Step.Key.Should().Be("lesson-2");
        today.Step.Tasks.Single(x => x.Key == ExerciseTypes.LetterToSound).Progress.Should().Be(7);
        lessonOne.Step.State.Should().Be(StepState.Done);
        lessonOne.Step.AllTasksDone.Should().BeTrue();
    }

    [Fact]
    public async Task GetPlan_ListsStepsByStageWithTheirState()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("lesson-5");

        var plan = await t.Today.GetPlanAsync(Ct);

        plan.CurrentKey.Should().Be("lesson-5");
        plan.Steps.Should().HaveCount(new Roadmap().Steps.Count);
        plan.Steps.Take(4).Should().OnlyContain(s => s.State == StepState.Done);
        plan.Steps.Single(s => s.Key == "lesson-5").State.Should().Be(StepState.Current);
        plan.Stages.Single(s => s.Stage == PlanStage.Hangul).DoneCount.Should().Be(3);
        plan.Stages.Single(s => s.Stage == PlanStage.Level1).StepCount.Should().Be(Roadmap.Level1Blocks);
        plan.Lessons.Should().HaveCount(Roadmap.LessonCount);
    }

    [Fact]
    public async Task UnknownOpenStep_ResumesAfterTheFurthestKnownStep()
    {
        await using var t = await TestDatabase.CreateAsync();
        t.Db.PlanSteps.Add(new PlanStepProgress { StepKey = "lesson-1", StartedUtc = t.Clock.UtcNow.AddDays(-2), CompletedUtc = t.Clock.UtcNow.AddDays(-1) });
        t.Db.PlanSteps.Add(new PlanStepProgress { StepKey = "renamed-step", StartedUtc = t.Clock.UtcNow.AddDays(-1) });
        await t.Db.SaveChangesAsync(Ct);

        var today = await t.Today.GetTodayAsync(Ct);

        today.Step.Key.Should().Be("lesson-2");
    }

    [Fact]
    public async Task ReadingTwice_StaysDone()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Today.MarkReadAsync(new MarkReadRequest(StudyMaterials.HangulGuide(1)), Ct);
        await t.Today.MarkReadAsync(new MarkReadRequest(StudyMaterials.HangulGuide(1)), Ct);

        (await t.Today.GetTodayAsync(Ct)).Step.Tasks.Single(x => x.Key == "guide").IsDone.Should().BeTrue();
    }

    private static List<AttemptDto> LetterAnswers(int count) => Answers(ExerciseTypes.LetterToSound, count);

    private static async Task DoLessonOneAsync(TestDatabase t)
    {
        await t.Today.MarkReadAsync(new MarkReadRequest(StudyMaterials.HangulGuide(1)), Ct);
        await t.Activity.RecordAttemptsAsync(
            [.. LetterAnswers(20), .. Answers(ExerciseTypes.SoundToLetter, 20), .. Answers(ExerciseTypes.Compose, 15), .. Answers(ExerciseTypes.Decompose, 15)], Ct);
        for (int i = 0; i < 10; i++)
        {
            await t.Activity.RecordReadingTrialAsync(new ReadingTrialDto("가나", 2, 1500, true), Ct);
        }
    }

    private static List<AttemptDto> Answers(string type, int count) =>
        [.. Enumerable.Range(0, count).Select(i => new AttemptDto(StudyModule.Hangul, type, $"{type}:{i}", true, 1500))];

    private static async Task IntroduceWordsAsync(TestDatabase t, int count)
    {
        var queue = await t.Vocabulary.GetQueueAsync(count, Ct);
        foreach (var card in queue.Cards.Take(count))
        {
            await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Domain.Srs.Grade.Good, 2000), Ct);
        }
    }
}
