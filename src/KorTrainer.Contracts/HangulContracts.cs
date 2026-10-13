namespace KorTrainer.Contracts;

public sealed record ReadingItemDto(string Text, int SyllableCount, string Pronounced, string? Translation, string Romanization);

public sealed record ReadingTrialDto(string Text, int SyllableCount, int ElapsedMs, bool ReadCorrectly);

public sealed record ReadingStatsDto(int Trials, double AverageMsPerSyllable, double Accuracy, double? TodayMsPerSyllable);

public sealed record SyllablePartsDto(string Syllable, string Initial, string Medial, string? Final);

public sealed record SoundChangeDto(string Rule, string Description);

public sealed record HangulAnalysisDto(
    string Text,
    string Pronounced,
    string Romanization,
    IReadOnlyList<SyllablePartsDto> Syllables,
    IReadOnlyList<SoundChangeDto> Changes);

public sealed record JamoDto(string Jamo, string Kind, string Sound, string Hint, string? Name, string SampleSyllable, string Romanization);

public sealed record HangulGuideDto(int Level, string Title, IReadOnlyList<string> KeyPoints, IReadOnlyList<JamoDto> Letters);
