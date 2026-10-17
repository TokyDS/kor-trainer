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
        var step = (await t.Today.GetTodayAsync(Ct)).Step;

        foreach (var task in step.Tasks)
        {
            await t.Today.SetTaskDoneAsync(task.Key, new SetTaskDoneRequest(step.Key, true), Ct);
        }

        var today = await t.Today.GetTodayAsync(Ct);
        today.ReadyForNext.Should().BeTrue();
        today.Step.NextTitle.Should().Contain("Урок 2");
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
        next.Step.Title.Should().Be("Блок 1 из 8");
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
        var tick = () => t.Today.SetTaskDoneAsync("words", new SetTaskDoneRequest("level1-2", true), Ct);

        preview.Step.State.Should().Be(StepState.Upcoming);
        preview.IsCurrent.Should().BeFalse();
        preview.CurrentKey.Should().Be("lesson-1");
        preview.Step.Tasks.Should().OnlyContain(task => task.Progress == 0);
        await tick.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task UnknownStepOrTask_IsNotFound()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => t.Today.GetStepAsync("day-42", Ct)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => t.Today.SetTaskDoneAsync("nope", new SetTaskDoneRequest("lesson-1", true), Ct))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetTaskDone_TicksAndUnticks()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Today.SetTaskDoneAsync("guide", new SetTaskDoneRequest("lesson-1", true), Ct);
        var ticked = await t.Today.GetTodayAsync(Ct);
        await t.Today.SetTaskDoneAsync("guide", new SetTaskDoneRequest("lesson-1", false), Ct);
        var unticked = await t.Today.GetTodayAsync(Ct);

        ticked.Step.Tasks.Single(x => x.Key == "guide").IsDone.Should().BeTrue();
        unticked.Step.Tasks.Single(x => x.Key == "guide").IsDone.Should().BeFalse();
    }

    [Fact]
    public async Task Blocks_MeasureTheWholeDeck()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("level1-1");
        await IntroduceWordsAsync(t, 3);

        var words = (await t.Today.GetTodayAsync(Ct)).Step.Tasks.Single(x => x.Key == "words");

        words.Progress.Should().Be(3);
        words.Target.Should().Be(100);
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
    public async Task TickingTwice_StaysTicked()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Today.SetTaskDoneAsync("guide", new SetTaskDoneRequest("lesson-1", true), Ct);
        await t.Today.SetTaskDoneAsync("guide", new SetTaskDoneRequest("lesson-1", true), Ct);

        (await t.Today.GetTodayAsync(Ct)).Step.Tasks.Single(x => x.Key == "guide").IsDone.Should().BeTrue();
    }

        private static List<AttemptDto> LetterAnswers(int count) => Answers(ExerciseTypes.LetterToSound, count);

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
