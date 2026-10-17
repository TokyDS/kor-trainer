using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Application.Listening;
using KorTrainer.Domain.Common;

namespace KorTrainer.Application.Tests;

public sealed class ListeningServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetExercises_PlaysRecordedWordsWhenEnoughAreAvailable()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(12);
        var recorded = new FakeAudioLibrary("가다", "바다", "학교", "사람", "시간", "친구", "음식", "가방", "의자", "책");
        var service = new ListeningService(t.Db, t.Settings, t.Clock, new Random(1), recorded);

        var set = await service.GetExercisesAsync(ExerciseTypes.ListenMeaning, 20, Ct);

        set.Module.Should().Be(StudyModule.Listening);
        set.Items.Should().HaveCount(20).And.OnlyContain(i => recorded.Words.Contains(i.SpeakText!));
    }

    [Fact]
    public async Task GetExercises_WithoutRecordings_UsesWordsOfOpenedStages()
    {
        await using var t = await TestDatabase.CreateAsync();
        var service = new ListeningService(t.Db, t.Settings, t.Clock, new Random(2), new FakeAudioLibrary());

        var set = await service.GetExercisesAsync(ExerciseTypes.ListenSpelling, 10, Ct);

        set.Items.Should().HaveCount(10).And.OnlyContain(i => i.Options.Count == 4 && i.AutoSpeak);
    }

    [Fact]
    public async Task GetExercises_RejectsUnknownType()
    {
        await using var t = await TestDatabase.CreateAsync();
        var service = new ListeningService(t.Db, t.Settings, t.Clock, new Random(3), new FakeAudioLibrary());

        var act = () => service.GetExercisesAsync("dictation", 5, Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }
}
