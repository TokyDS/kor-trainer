using System.Text;
using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Hangul;
using KorTrainer.Domain.Plan;
using KorTrainer.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KorTrainer.Application.Tests;

public sealed class DeckAndClockTests
{
    private static readonly IReadOnlyList<DeckEntry> Deck = DeckLoader.LoadBundled();

    [Fact]
    public void BundledDeck_HasAboutThreeHundredWordsWithUniqueKeys()
    {
        Deck.Count.Should().BeInRange(300, 400);
        Deck.Select(e => e.DeckKey).Should().OnlyHaveUniqueItems();
        Deck.Select(e => e.Rank).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void BundledDeck_GivesHomonymsNumberedKeys()
    {
        Deck.Where(e => e.Korean == "눈").Select(e => e.DeckKey).Should().Equal("눈#1", "눈#2");
        Deck.Single(e => e.DeckKey == "눈#2").Russian.Should().Be("снег");
    }

    [Fact]
    public void BundledDeck_LessonWordsCoverDaysFourToNine()
    {
        var lessonDays = Deck.Where(e => e.Stage == PlanStage.FirstPhrases).Select(e => e.LessonDay).Distinct();

        lessonDays.Should().BeEquivalentTo(new int?[] { 4, 5, 6, 7, 8, 9 });
        Deck.Where(e => e.Stage != PlanStage.FirstPhrases).Should().OnlyContain(e => e.LessonDay == null);
    }

    [Fact]
    public void BundledDeck_ContainsOnlyHangulAndCompleteTranslations()
    {
        Deck.Should().OnlyContain(e => e.Korean.Any(Syllable.IsSyllable) && e.Korean.All(c => Syllable.IsSyllable(c) || c == ' ' || c == '?'));
        Deck.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Russian) && !string.IsNullOrWhiteSpace(e.Topic));
    }

    [Fact]
    public void BundledDeck_MarksKnownIrregularVerbs()
    {
        Deck.Single(e => e.Korean == "듣다").Irregularity.Should().Be(Irregularity.Digeut);
        Deck.Single(e => e.Korean == "덥다").Irregularity.Should().Be(Irregularity.Bieup);
        Deck.Single(e => e.Korean == "모르다").Irregularity.Should().Be(Irregularity.Reu);
        Deck.Single(e => e.Korean == "입다").Irregularity.Should().Be(Irregularity.None);
    }

    [Fact]
    public void DeckLoader_RejectsMalformedEntries()
    {
        const string json = """{ "version": 1, "sections": [ { "stage": "Level1", "topic": "x", "words": [["가"]] } ] }""";

        var act = () => DeckLoader.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public async Task SyncDeck_UpdatesTranslationsInPlaceKeepingProgress()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = await t.Db.Words.SingleAsync(w => w.DeckKey == "학교#1");
        t.Db.WordProgress.Add(new WordProgress { WordId = word.Id, DueUtc = DateTime.UtcNow, IntroducedUtc = DateTime.UtcNow, LastReviewedUtc = DateTime.UtcNow });
        await t.Db.SaveChangesAsync();
        var edited = Deck.Select(e => e.DeckKey == "학교#1" ? e with { Russian = "школа (исправлено)" } : e).ToList();

        await new Infrastructure.Persistence.DatabaseInitializer(t.Db, NullLogger<Infrastructure.Persistence.DatabaseInitializer>.Instance)
            .SyncDeckAsync(edited, CancellationToken.None);

        (await t.Db.Words.SingleAsync(w => w.DeckKey == "학교#1")).Russian.Should().Be("школа (исправлено)");
        (await t.Db.WordProgress.CountAsync(p => p.WordId == word.Id)).Should().Be(1);
    }

    [Fact]
    public void StudyClock_ConvertsToLearnerLocalDayAndDayEnd()
    {
        var settings = new UserSettings { TimeZoneId = "Asia/Seoul" };

        StudyClock.LocalDate(settings, new DateTime(2026, 10, 13, 16, 0, 0, DateTimeKind.Utc)).Should().Be(new DateOnly(2026, 10, 14));
        StudyClock.EndOfLocalDayUtc(settings, new DateOnly(2026, 10, 14)).Should().Be(new DateTime(2026, 10, 14, 15, 0, 0));
    }

    [Fact]
    public void StudyClock_HandlesMidnightDaylightSavingGap()
    {
        // Chile springs forward at 00:00 on the first Sunday of September: 2026-09-06 00:00 does not exist.
        var settings = new UserSettings { TimeZoneId = "America/Santiago" };

        var end = StudyClock.EndOfLocalDayUtc(settings, new DateOnly(2026, 9, 5));

        end.Should().Be(new DateTime(2026, 9, 6, 4, 0, 0));
    }

    [Fact]
    public void StudyClock_FallsBackToUtcForUnknownZones()
    {
        var settings = new UserSettings { TimeZoneId = "Nowhere/Nothing" };

        StudyClock.LocalDate(settings, new DateTime(2026, 10, 13, 23, 30, 0, DateTimeKind.Utc)).Should().Be(new DateOnly(2026, 10, 13));
        StudyClock.IsKnownZone("Nowhere/Nothing").Should().BeFalse();
    }
}
