using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Entities;

public enum ThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>Single-row settings of the (only) learner.</summary>
public sealed class UserSettings
{
    public const int SingletonId = 1;
    public const string DefaultTimeZoneId = "Europe/Moscow";

    public int Id { get; set; } = SingletonId;

    public static readonly DateOnly DefaultStartDate = new(2026, 10, 13);

    /// <summary>When the learner began; the plan itself is self-paced, this is kept for statistics and old backups.</summary>
    public DateOnly StartDate { get; set; } = DefaultStartDate;

    /// <summary>IANA time zone that defines where a study day begins and ends.</summary>
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;

    public bool ShowRomanization { get; set; }

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public int NewWordsPerDay { get; set; } = 10;

    public int MaxReviewsPerDay { get; set; } = 200;

    public double SpeechRate { get; set; } = 0.9;

    /// <summary>A gentle evening nudge (Today banner, desktop and phone notification) on days without study.</summary>
    public bool RemindersEnabled { get; set; } = true;

    /// <summary>
    /// Play Lingua Libre recordings where they exist. Off by default so every word and sentence is spoken by
    /// the same synthesized voice.
    /// </summary>
    public bool NativeRecordings { get; set; }
}
