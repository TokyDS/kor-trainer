namespace KorTrainer.Contracts;

/// <param name="StudiedWords">Words with this root the learner has already met.</param>
/// <param name="Examples">A few of its words, studied ones first.</param>
public sealed record HanjaRootDto(string Character, string Reading, string? Hun, string? Russian, int StudiedWords, int TotalWords, IReadOnlyList<string> Examples);

public sealed record HanjaWordDto(int WordId, string Korean, string Russian, string Hanja, bool Studied);

public sealed record HanjaRootDetailDto(HanjaRootDto Root, IReadOnlyList<HanjaWordDto> Words);
