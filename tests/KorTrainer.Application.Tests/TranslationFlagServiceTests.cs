using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class TranslationFlagServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Word> WordAsync(TestDatabase t) => await t.Db.Words.AsNoTracking().OrderBy(w => w.Id).FirstAsync(Ct);

    [Fact]
    public async Task Flag_KeepsTheTranslationAndTheNote_AndShowsOnTheWord()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = await WordAsync(t);

        var flag = await t.TranslationFlags.FlagAsync(word.Id, new FlagTranslationRequest("  скорее «дом»  "), Ct);

        flag.Should().Be(new TranslationFlagDto(word.Id, word.Korean, word.Russian, word.Russian, "скорее «дом»", t.Clock.UtcNow));
        flag.IsCorrected.Should().BeFalse();
        (await t.TranslationFlags.GetAllAsync(Ct)).Should().ContainSingle().Which.Should().Be(flag);
        (await t.WordDetails.GetAsync(word.Id, Ct)).Flag.Should().Be(flag);
    }

    [Fact]
    public async Task FlagAgain_ReplacesTheNote_EmptyNoteIsNone()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = await WordAsync(t);
        await t.TranslationFlags.FlagAsync(word.Id, new FlagTranslationRequest("первая мысль"), Ct);

        var flag = await t.TranslationFlags.FlagAsync(word.Id, new FlagTranslationRequest(" "), Ct);

        flag.Note.Should().BeNull();
        (await t.Db.TranslationFlags.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task CorrectedWord_IsToldApart()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = await WordAsync(t);
        await t.TranslationFlags.FlagAsync(word.Id, new FlagTranslationRequest(null), Ct);

        await t.Db.Words.Where(w => w.Id == word.Id).ExecuteUpdateAsync(s => s.SetProperty(w => w.Russian, "исправлено"), Ct);

        var flag = (await t.TranslationFlags.GetAllAsync(Ct)).Single();
        flag.Current.Should().Be("исправлено");
        flag.IsCorrected.Should().BeTrue();
    }

    [Fact]
    public async Task Unflag_RemovesIt_AndIsQuietWhenThereIsNone()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = await WordAsync(t);
        await t.TranslationFlags.FlagAsync(word.Id, new FlagTranslationRequest(null), Ct);

        await t.TranslationFlags.UnflagAsync(word.Id, Ct);
        await t.TranslationFlags.UnflagAsync(word.Id, Ct);

        (await t.TranslationFlags.GetAllAsync(Ct)).Should().BeEmpty();
        (await t.WordDetails.GetAsync(word.Id, Ct)).Flag.Should().BeNull();
    }

    [Fact]
    public async Task Flag_RejectsUnknownWordsAndLongNotes()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = await WordAsync(t);

        var unknown = () => t.TranslationFlags.FlagAsync(-1, new FlagTranslationRequest(null), Ct);
        var tooLong = () => t.TranslationFlags.FlagAsync(word.Id, new FlagTranslationRequest(new string('а', TranslationFlagLimits.MaxNoteLength + 1)), Ct);

        await unknown.Should().ThrowAsync<NotFoundException>();
        await tooLong.Should().ThrowAsync<ValidationFailedException>();
    }
}
