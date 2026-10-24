using AwesomeAssertions;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class TaskOrderTests
{
    [Fact]
    public void Locked_OpensTheFirstUnfinishedTaskOnly()
    {
        var locked = TaskOrder.Locked([new(true, true), new(false, false), new(false, false)]);

        locked.Should().Equal(false, false, true);
    }

    [Fact]
    public void Locked_KeepsAStartedTaskOpen()
    {
        // Practice from the grammar hub counts even out of order; such a task is not taken away.
        var locked = TaskOrder.Locked([new(false, false), new(false, true), new(false, false)]);

        locked.Should().Equal(false, false, true);
    }

    [Fact]
    public void Locked_NeverLocksDoneTasks()
    {
        var locked = TaskOrder.Locked([new(false, true), new(true, true), new(true, true)]);

        locked.Should().Equal(false, false, false);
    }

    [Fact]
    public void Locked_IsEmptyForNoTasks() => TaskOrder.Locked([]).Should().BeEmpty();
}
