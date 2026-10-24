using AwesomeAssertions;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Domain.Tests.Srs;

public sealed class MistakeScheduleTests
{
    private static readonly DateOnly Day = new(2026, 10, 22);

    [Fact]
    public void Missed_ComesBackTomorrow() =>
        MistakeSchedule.Missed(Day).Should().Be(new MistakeState(0, Day.AddDays(1), null));

    [Fact]
    public void CorrectReviews_WalkOneThreeSevenDays_ThenClose()
    {
        var state = MistakeSchedule.Missed(Day);

        state = MistakeSchedule.Answered(state, correct: true, Day.AddDays(1));
        state.Should().Be(new MistakeState(1, Day.AddDays(4), null));

        state = MistakeSchedule.Answered(state, correct: true, Day.AddDays(4));
        state.Should().Be(new MistakeState(2, Day.AddDays(11), null));

        state = MistakeSchedule.Answered(state, correct: true, Day.AddDays(12));
        state.Resolved.Should().Be(Day.AddDays(12));
    }

    [Fact]
    public void CorrectAnswerBeforeTheDueDay_ChangesNothing()
    {
        var state = MistakeSchedule.Missed(Day);

        MistakeSchedule.Answered(state, correct: true, Day).Should().Be(state);
    }

    [Fact]
    public void WrongAnswer_StartsOver_EvenWhenClosed()
    {
        var state = new MistakeState(2, Day, null);

        MistakeSchedule.Answered(state, correct: false, Day).Should().Be(MistakeSchedule.Missed(Day));
        MistakeSchedule.Answered(state with { Resolved = Day }, correct: false, Day.AddDays(30))
            .Should().Be(MistakeSchedule.Missed(Day.AddDays(30)));
    }
}
