using KorTrainer.Domain.Entities;

namespace KorTrainer.Contracts;

public sealed record SettingsDto(
    DateOnly StartDate,
    string TimeZoneId,
    bool ShowRomanization,
    ThemePreference Theme,
    int NewWordsPerDay,
    int MaxReviewsPerDay,
    double SpeechRate,
    bool RemindersEnabled = true,
    bool NativeRecordings = false,
    bool ShowCompanions = true);
