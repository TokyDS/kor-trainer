using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class StudyPlanTests
{
    private readonly StudyPlan _plan = new(StudyPlan.DefaultStartDate);

    [Theory]
    [InlineData("2026-10-12", PlanStage.NotStarted)]
    [InlineData("2026-10-13", PlanStage.Hangul)]
    [InlineData("2026-10-15", PlanStage.Hangul)]
    [InlineData("2026-10-16", PlanStage.FirstPhrases)]
    [InlineData("2026-10-22", PlanStage.FirstPhrases)]
    [InlineData("2026-10-23", PlanStage.Level1)]
    [InlineData("2026-12-31", PlanStage.Level1)]
    [InlineData("2027-01-01", PlanStage.Level2)]
    [InlineData("2027-02-21", PlanStage.Level2)]
    [InlineData("2027-02-22", PlanStage.MockExam)]
    [InlineData("2027-02-28", PlanStage.MockExam)]
    [InlineData("2027-03-01", PlanStage.TopikPrep)]
    [InlineData("2027-04-30", PlanStage.TopikPrep)]
    [InlineData("2027-05-01", PlanStage.Maintenance)]
    public void StageOn_FollowsThePlanCalendar(string date, PlanStage expected) =>
        _plan.StageOn(DateOnly.Parse(date)).Should().Be(expected);

    [Fact]
    public void DayNumber_StartsAtOneOnStartDate()
    {
        _plan.DayNumber(new DateOnly(2026, 10, 13)).Should().Be(1);
        _plan.DayNumber(new DateOnly(2026, 10, 22)).Should().Be(10);
    }

    [Fact]
    public void LaterStartDate_ShiftsLessonDaysButKeepsCalendarStages()
    {
        var plan = new StudyPlan(new DateOnly(2026, 12, 28));

        plan.StageOn(new DateOnly(2026, 12, 30)).Should().Be(PlanStage.Hangul);
        plan.StageOn(new DateOnly(2027, 1, 5)).Should().Be(PlanStage.FirstPhrases);
        plan.StageOn(new DateOnly(2027, 1, 7)).Should().Be(PlanStage.Level2);
        plan.Stages.Select(s => s.Stage).Should().NotContain(PlanStage.Level1);
    }

    [Fact]
    public void Stages_AreContiguousForDefaultStart()
    {
        var stages = _plan.Stages;

        stages.Select(s => s.Stage).Should().Equal(
            PlanStage.Hangul, PlanStage.FirstPhrases, PlanStage.Level1, PlanStage.Level2, PlanStage.MockExam, PlanStage.TopikPrep);
        for (int i = 1; i < stages.Count; i++)
        {
            stages[i].Start.Should().Be(stages[i - 1].End.AddDays(1));
        }
    }

    [Fact]
    public void GetDay_OnDayOne_HasHangulLessonAndDrills()
    {
        var day = _plan.GetDay(new DateOnly(2026, 10, 13));

        day.DayNumber.Should().Be(1);
        day.Lesson!.Title.Should().Contain("базовые");
        day.Tasks.Should().Contain(t => t.Target.Module == StudyModule.Hangul && t.Target.ExerciseType == ExerciseTypes.LetterToSound);
        day.Tasks.Select(t => t.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryLessonDay_HasTasksWithUniqueKeys()
    {
        for (int day = 1; day <= StudyPlan.FirstPhrasesLastDay; day++)
        {
            var planDay = _plan.GetDay(StudyPlan.DefaultStartDate.AddDays(day - 1));

            planDay.Lesson.Should().NotBeNull();
            planDay.Tasks.Should().NotBeEmpty();
            planDay.Tasks.Select(t => t.Key).Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void GetDay_InLevel1_IncludesReviewAndNewWords()
    {
        var day = new StudyPlan(StudyPlan.DefaultStartDate, newWordsPerDay: 12).GetDay(new DateOnly(2026, 11, 20));

        day.Lesson.Should().BeNull();
        day.Tasks.Should().Contain(t => t.Target.Metric == TaskMetric.VocabularyDueCleared);
        day.Tasks.Should().Contain(t => t.Target.Metric == TaskMetric.NewWords && t.Target.Count == 12);
    }

    [Fact]
    public void GetDay_OnLastMockExamDay_AddsDecisionTask()
    {
        _plan.GetDay(StudyPlan.MockExamEnd).Tasks.Should().Contain(t => t.Key == "decide");
        _plan.GetDay(StudyPlan.MockExamStart).Tasks.Should().NotContain(t => t.Key == "decide");
    }

    [Fact]
    public void GetDay_InTopikPrep_RotatesWritingTasks()
    {
        var routes = Enumerable.Range(0, 3)
            .Select(i => _plan.GetDay(StudyPlan.TopikPrepStart.AddDays(i)).Tasks.Single(t => t.Key == "writing").Route)
            .ToList();

        routes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetDay_BeforeStart_HasNoTasks()
    {
        _plan.GetDay(new DateOnly(2026, 10, 1)).Tasks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("2026-10-14", PlanStage.FirstPhrases, 4, false)]
    [InlineData("2026-10-16", PlanStage.FirstPhrases, 4, true)]
    [InlineData("2026-10-16", PlanStage.FirstPhrases, 5, false)]
    [InlineData("2026-10-16", PlanStage.Level1, null, false)]
    [InlineData("2026-11-01", PlanStage.FirstPhrases, 9, true)]
    [InlineData("2026-11-01", PlanStage.Level1, null, true)]
    [InlineData("2026-11-01", PlanStage.Level2, null, false)]
    [InlineData("2027-02-25", PlanStage.Level2, null, true)]
    [InlineData("2027-03-02", PlanStage.TopikPrep, null, true)]
    public void WordUnlockOn_OpensWordsByStageAndLessonDay(string date, PlanStage wordStage, int? lessonDay, bool expected) =>
        _plan.WordUnlockOn(DateOnly.Parse(date)).IsUnlocked(wordStage, lessonDay).Should().Be(expected);
}
