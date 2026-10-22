using AwesomeAssertions;
using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Tests.Common;

public sealed class WeakTopicsTests
{
    [Fact]
    public void Rank_PutsTheLeastAccurateFirst_AndKeepsThree()
    {
        TopicTally[] tallies =
        [
            new(StudyModule.Grammar, ExerciseTypes.Past, 20, 15),
            new(StudyModule.Grammar, ExerciseTypes.Future, 20, 10),
            new(StudyModule.Numbers, ExerciseTypes.Counters, 12, 6),
            new(StudyModule.Hangul, ExerciseTypes.ConsonantSeries, 30, 24),
            new(StudyModule.Grammar, ExerciseTypes.If, 10, 8),
        ];

        WeakTopics.Rank(tallies).Select(t => t.Type)
            .Should().Equal(ExerciseTypes.Future, ExerciseTypes.Counters, ExerciseTypes.Past);
    }

    [Fact]
    public void Rank_IgnoresGoodTopics_FewAnswers_MixedSetsAndOtherModules()
    {
        TopicTally[] tallies =
        [
            new(StudyModule.Grammar, ExerciseTypes.Past, 40, 38),
            new(StudyModule.Grammar, ExerciseTypes.Future, 9, 0),
            new(StudyModule.Grammar, ExerciseTypes.Mixed, 50, 10),
            new(StudyModule.Listening, ExerciseTypes.ListenMeaning, 50, 10),
        ];

        WeakTopics.Rank(tallies).Should().BeEmpty();
    }
}
