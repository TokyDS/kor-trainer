using System.Text;
using AwesomeAssertions;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KorTrainer.Embedded.Tests;

/// <summary>The in-process back end of the Android app, on a real SQLite file like on the phone.</summary>
public sealed class LocalKorTrainerApiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kortrainer-embedded-" + Guid.NewGuid().ToString("N"));
    private readonly List<ServiceProvider> _providers = [];

    [Fact]
    public async Task Settings_SurviveAnAppRestart()
    {
        var api = StartApp();
        var settings = await api.GetSettingsAsync();

        await api.SaveSettingsAsync(settings with { ShowRomanization = true, NewWordsPerDay = 15 });
        var reloaded = await StartApp().GetSettingsAsync();

        reloaded.ShowRomanization.Should().BeTrue();
        reloaded.NewWordsPerDay.Should().Be(15);
    }

    [Fact]
    public async Task InvalidInput_GivesTheServersMessage()
    {
        var api = StartApp();
        var settings = await api.GetSettingsAsync();

        var act = () => api.SaveSettingsAsync(settings with { StartDate = new DateOnly(2000, 1, 1) });

        var error = (await act.Should().ThrowAsync<ApiException>()).Which;
        error.StatusCode.Should().Be(400);
        error.Message.Should().StartWith("Проверьте введённые данные.").And.Contain("Дата старта");
    }

    [Fact]
    public async Task UnknownGrammarTopic_IsAValidationError()
    {
        var act = () => StartApp().GetGrammarExercisesAsync("no-such-topic", 5);

        (await act.Should().ThrowAsync<ApiException>()).Which.Message.Should().Contain("Неизвестная тема грамматики.");
    }

    [Fact]
    public async Task ConcurrentCalls_UseSeparateDbContexts()
    {
        var api = StartApp();

        var calls = new Task[] { api.GetTodayAsync(), api.GetVocabularySummaryAsync(), api.GetStatsOverviewAsync(), api.GetPlanAsync(), api.GetQueueAsync() };

        await FluentActions.Awaiting(() => Task.WhenAll(calls)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Backup_MovesProgressToAnotherDevice()
    {
        var phone = StartApp();
        await phone.RecordAttemptsAsync([new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "ㄱ+ㅏ", true, 900)]);
        byte[] backup = await ((LocalKorTrainerApi)phone).ExportProgressAsync();

        var otherPhone = StartApp(Path.Combine(_root, "other"));
        var result = await otherPhone.ImportProgressAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        result.Attempts.Should().Be(1);
        (await otherPhone.GetStatsOverviewAsync()).Should().NotBeNull();
        Encoding.UTF8.GetString(backup).Should().Contain("\"module\": \"Hangul\"", "enums are written by name and the file is indented");
    }

    [Fact]
    public async Task Import_RejectsAFileThatIsNotABackup()
    {
        var act = () => StartApp().ImportProgressAsync(new MemoryStream("not json"u8.ToArray()), TestContext.Current.CancellationToken);

        var error = (await act.Should().ThrowAsync<ApiException>()).Which;
        error.StatusCode.Should().Be(400);
        error.Message.Should().Be("Файл не похож на резервную копию KorTrainer.");
    }

    [Fact]
    public async Task RecordedWords_ComeFromTheBundledManifest()
    {
        var words = await StartApp().GetRecordedWordsAsync();

        words.Should().NotBeEmpty().And.BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void UnexpectedErrors_DoNotLeakDetails()
    {
        var api = (LocalKorTrainerApi)StartApp();

        var error = api.Translate(new InvalidOperationException("SQLite Error 1: no such table"));

        error.StatusCode.Should().Be(500);
        error.Message.Should().Be("Внутренняя ошибка приложения.");
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private IKorTrainerApi StartApp(string? dataDirectory = null)
    {
        string data = dataDirectory ?? Path.Combine(_root, "data");
        Directory.CreateDirectory(data);
        var provider = new ServiceCollection().AddEmbeddedTrainer(data, Path.Combine(_root, "cache")).BuildServiceProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<IKorTrainerApi>();
    }
}
