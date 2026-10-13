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

    public DateOnly StartDate { get; set; } = StudyPlan.DefaultStartDate;

    /// <summary>IANA time zone that defines where a study day begins and ends.</summary>
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;

    public bool ShowRomanization { get; set; }

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public int NewWordsPerDay { get; set; } = 10;

    public int MaxReviewsPerDay { get; set; } = 200;

    public double SpeechRate { get; set; } = 0.9;
}
