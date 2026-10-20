using KorTrainer.Application.Common;
using KorTrainer.Application.Plan;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Reading;

/// <summary>Graded reading: original short texts whose words link to the deck for tap-to-translate.</summary>
public sealed class ReadingService(IAppDbContext db, ITextLibrary library, IWordIndexProvider words, PlanCursor cursor)
{
    public const string AttemptPrefix = "text:";

    public async Task<IReadOnlyList<TextSummaryDto>> GetTextsAsync(CancellationToken cancellationToken)
    {
        var stage = (await cursor.GetAsync(cancellationToken)).Step.Stage;
        var studied = await StudiedKeysAsync(cancellationToken);
        var read = (await db.ExerciseAttempts
                .Where(a => a.Module == StudyModule.Reading && a.ItemKey.StartsWith(AttemptPrefix))
                .Select(a => a.ItemKey)
                .Distinct()
                .ToListAsync(cancellationToken))
            .Select(TextIdOf)
            .ToHashSet(StringComparer.Ordinal);

        return library.Texts.Select(t =>
        {
            var keys = t.Sentences.SelectMany(s => words.Index.Analyze(s.Korean)).Where(k => k.IsKnown).Select(k => k.Words[0].Key).Distinct().ToList();
            int known = keys.Count == 0 ? 0 : keys.Count(studied.Contains) * 100 / keys.Count;
            return new TextSummaryDto(t.Id, t.Stage, Roadmap.StageTitle(t.Stage), t.Title, t.TitleKorean, t.Sentences.Count, IsOpen(t.Stage, stage), read.Contains(t.Id), known);
        }).ToList();
    }

    public async Task<ReadingTextDto> GetTextAsync(string id, CancellationToken cancellationToken)
    {
        var text = library.Texts.FirstOrDefault(t => t.Id == id) ?? throw new NotFoundException("Такого текста нет.");
        var analyzed = text.Sentences.Select(s => (Sentence: s, Tokens: words.Index.Analyze(s.Korean))).ToList();
        var keys = analyzed.SelectMany(a => a.Tokens).Where(t => t.IsKnown).Select(t => t.Words[0].Key).Distinct().ToList();
        var deck = await db.Words
            .Where(w => keys.Contains(w.DeckKey))
            .Select(w => new { w.Id, w.DeckKey, w.Korean, w.Russian, Studied = w.Progress != null })
            .ToDictionaryAsync(w => w.DeckKey, StringComparer.Ordinal, cancellationToken);

        var sentences = analyzed.Select(a => new TextSentenceDto(
            a.Sentence.Korean,
            a.Sentence.Russian,
            a.Tokens.Select(t => t.IsKnown && deck.TryGetValue(t.Words[0].Key, out var w)
                    ? new TextTokenDto(t.Text, w.Id, w.Korean, w.Russian, w.Studied)
                    : new TextTokenDto(t.Text, null, null, null, false))
                .ToList()))
            .ToList();
        return new ReadingTextDto(
            text.Id,
            text.Stage,
            Roadmap.StageTitle(text.Stage),
            text.Title,
            text.TitleKorean,
            sentences,
            text.Questions.Select(q => new TextQuestionDto(q.Question, q.Options, q.Answer)).ToList());
    }

    /// <summary>"text:{id}:{question}" → id.</summary>
    private static string TextIdOf(string itemKey)
    {
        string rest = itemKey[AttemptPrefix.Length..];
        int colon = rest.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? rest : rest[..colon];
    }

    private static bool IsOpen(PlanStage text, PlanStage current) =>
        text <= (current < PlanStage.FirstPhrases ? PlanStage.FirstPhrases : current);

    private async Task<HashSet<string>> StudiedKeysAsync(CancellationToken cancellationToken) =>
        (await db.WordProgress.Select(p => p.Word!.DeckKey).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
}
