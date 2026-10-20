using KorTrainer.Domain.Plan;

namespace KorTrainer.Contracts;

/// <param name="IsOpen">The plan has reached the text's stage (it can be read anyway).</param>
/// <param name="IsRead">Its questions were answered at least once.</param>
/// <param name="KnownPercent">Share of its words the learner has studied.</param>
public sealed record TextSummaryDto(string Id, PlanStage Stage, string StageTitle, string Title, string TitleKorean, int Sentences, bool IsOpen, bool IsRead, int KnownPercent);

/// <param name="Text">The token as written (with punctuation).</param>
/// <param name="WordId">The deck word it was recognized as, if any.</param>
/// <param name="Lemma">Dictionary form of that word (먹었어요 → 먹다).</param>
public sealed record TextTokenDto(string Text, int? WordId, string? Lemma, string? Russian, bool Studied);

public sealed record TextSentenceDto(string Korean, string Russian, IReadOnlyList<TextTokenDto> Tokens);

public sealed record TextQuestionDto(string Question, IReadOnlyList<string> Options, int Answer);

public sealed record ReadingTextDto(string Id, PlanStage Stage, string StageTitle, string Title, string TitleKorean, IReadOnlyList<TextSentenceDto> Sentences, IReadOnlyList<TextQuestionDto> Questions);
