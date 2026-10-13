using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Settings;

public sealed class SettingsService(IAppDbContext db)
{
    public const int MaxNewWordsPerDay = 100;
    public const int MinReviewsPerDay = 10;
    public const int MaxReviewsPerDay = 5000;
    public const double MinSpeechRate = 0.5;
    public const double MaxSpeechRate = 1.5;

    private static readonly DateOnly EarliestStart = new(2024, 1, 1);
    private static readonly DateOnly LatestStart = new(2030, 12, 31);

    /// <summary>Loads the settings row, creating it with defaults on first use.</summary>
    public async Task<UserSettings> GetEntityAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Settings.SingleOrDefaultAsync(s => s.Id == UserSettings.SingletonId, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new UserSettings();
        db.Settings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<SettingsDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await GetEntityAsync(cancellationToken));

    public async Task<SettingsDto> UpdateAsync(SettingsDto request, CancellationToken cancellationToken)
    {
        Validate(request);
        var settings = await GetEntityAsync(cancellationToken);
        Apply(settings, request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    public static void Validate(SettingsDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        new Validator()
            .Require(request.StartDate >= EarliestStart && request.StartDate <= LatestStart, nameof(request.StartDate), "Дата старта должна быть между 2024 и 2030 годом.")
            .Require(StudyClock.IsKnownZone(request.TimeZoneId), nameof(request.TimeZoneId), "Неизвестный часовой пояс.")
            .Require(Enum.IsDefined(request.Theme), nameof(request.Theme), "Неизвестная тема.")
            .Require(request.NewWordsPerDay is >= 1 and <= MaxNewWordsPerDay, nameof(request.NewWordsPerDay), $"Новых слов в день: от 1 до {MaxNewWordsPerDay}.")
            .Require(request.MaxReviewsPerDay is >= MinReviewsPerDay and <= MaxReviewsPerDay, nameof(request.MaxReviewsPerDay), $"Повторений в день: от {MinReviewsPerDay} до {MaxReviewsPerDay}.")
            .Require(request.SpeechRate is >= MinSpeechRate and <= MaxSpeechRate, nameof(request.SpeechRate), $"Скорость речи: от {MinSpeechRate} до {MaxSpeechRate}.")
            .ThrowIfInvalid();
    }

    public static void Apply(UserSettings settings, SettingsDto source)
    {
        settings.StartDate = source.StartDate;
        settings.TimeZoneId = source.TimeZoneId;
        settings.ShowRomanization = source.ShowRomanization;
        settings.Theme = source.Theme;
        settings.NewWordsPerDay = source.NewWordsPerDay;
        settings.MaxReviewsPerDay = source.MaxReviewsPerDay;
        settings.SpeechRate = source.SpeechRate;
    }

    public static SettingsDto ToDto(UserSettings s) =>
        new(s.StartDate, s.TimeZoneId, s.ShowRomanization, s.Theme, s.NewWordsPerDay, s.MaxReviewsPerDay, s.SpeechRate);
}
