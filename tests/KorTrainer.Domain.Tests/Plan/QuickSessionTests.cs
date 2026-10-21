using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class QuickSessionTests
{
    [Theory]
    [InlineData("/hangul/compose?level=2", StudyModule.Hangul, ExerciseTypes.Compose, 2)]
    [InlineData("/hangul/letter-to-sound?level=1", StudyModule.Hangul, ExerciseTypes.LetterToSound, 1)]
    [InlineData("/grammar/past", StudyModule.Grammar, ExerciseTypes.Past, 1)]
    [InlineData("/grammar/mixed", StudyModule.Grammar, ExerciseTypes.Mixed, 1)]
    [InlineData("/listening", StudyModule.Listening, ExerciseTypes.ListenMeaning, 1)]
    [InlineData("/listening/listen-spelling", StudyModule.Listening, ExerciseTypes.ListenSpelling, 1)]
    [InlineData("/numbers/mixed", StudyModule.Numbers, ExerciseTypes.Mixed, 1)]
    public void FromRoute_RecognisesChoiceDrills(string route, StudyModule module, string type, int level)
    {
        var drill = QuickSession.FromRoute(route);

        drill.Should().Be(new QuickDrill(module, type, level));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/vocab/review")]
    [InlineData("/hangul/reading?level=2")]
    [InlineData("/hangul/guide?level=1")]
    [InlineData("/listening/dictation")]
    [InlineData("/read")]
    [InlineData("/grammar/no-such-topic")]
    public void FromRoute_IgnoresEverythingElse(string? route) =>
        QuickSession.FromRoute(route).Should().BeNull();

    [Fact]
    public void FromRoute_ClampsTheHangulLevel() =>
        QuickSession.FromRoute("/hangul/compose?level=9")!.Level.Should().Be(3);

    [Fact]
    public void Drills_StartWithTheFirstUnfinishedStepTask_ThenListening()
    {
        QuickTask[] tasks =
        [
            new("/vocab/review", false),
            new("/grammar/past", true),
            new("/grammar/not", false),
            new("/numbers/mixed", false),
        ];

        var drills = QuickSession.Drills(PlanStage.Level1, tasks);

        drills.Should().Equal(
            new QuickDrill(StudyModule.Grammar, ExerciseTypes.Not),
            new QuickDrill(StudyModule.Listening, ExerciseTypes.ListenMeaning));
    }

    [Fact]
    public void Drills_UseAFinishedTask_WhenAllAreDone()
    {
        var drills = QuickSession.Drills(PlanStage.Level1, [new QuickTask("/grammar/past", true)]);

        drills[0].Should().Be(new QuickDrill(StudyModule.Grammar, ExerciseTypes.Past));
    }

    [Fact]
    public void Drills_FallBackToMixedGrammar()
    {
        var drills = QuickSession.Drills(PlanStage.TopikPrep, [new QuickTask("/read", false)]);

        drills[0].Should().Be(new QuickDrill(StudyModule.Grammar, ExerciseTypes.Mixed));
    }

    [Fact]
    public void Drills_StayWithLettersDuringHangulLessons()
    {
        var drills = QuickSession.Drills(PlanStage.Hangul, [new QuickTask("/hangul/compose?level=3", false)]);

        drills.Should().Equal(
            new QuickDrill(StudyModule.Hangul, ExerciseTypes.Compose, 3),
            new QuickDrill(StudyModule.Hangul, ExerciseTypes.SoundToLetter, 3));
    }

    [Fact]
    public void Drills_PickAnotherLetterDrill_WhenTheStepTaskIsAlreadySoundToLetter()
    {
        var drills = QuickSession.Drills(PlanStage.Hangul, [new QuickTask("/hangul/sound-to-letter?level=1", false)]);

        drills[1].Should().Be(new QuickDrill(StudyModule.Hangul, ExerciseTypes.LetterToSound));
    }

    [Fact]
    public void Drills_DoNotRepeatListening_WhenTheStepTaskIsListening()
    {
        var drills = QuickSession.Drills(PlanStage.Level2, [new QuickTask("/listening", false)]);

        drills.Should().Equal(
            new QuickDrill(StudyModule.Listening, ExerciseTypes.ListenMeaning),
            new QuickDrill(StudyModule.Grammar, ExerciseTypes.Mixed));
    }
}
