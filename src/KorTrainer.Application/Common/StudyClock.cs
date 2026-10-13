using KorTrainer.Domain.Entities;

namespace KorTrainer.Application.Common;

/// <summary>Translates server UTC time into the learner's study calendar.</summary>
public sealed class StudyClock(TimeProvider timeProvider)
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public static TimeZoneInfo ResolveZone(string timeZoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;

    public static bool IsKnownZone(string timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    public DateOnly Today(UserSettings settings) => LocalDate(settings, UtcNow);

    public static DateOnly LocalDate(UserSettings settings, DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ResolveZone(settings.TimeZoneId)));

    /// <summary>The UTC instant at which the given local day ends (next local midnight).</summary>
    public static DateTime EndOfLocalDayUtc(UserSettings settings, DateOnly date)
    {
        var zone = ResolveZone(settings.TimeZoneId);
        var nextMidnight = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // In zones that switch to DST at 00:00 (e.g. America/Santiago) local midnight does not exist.
        while (zone.IsInvalidTime(nextMidnight))
        {
            nextMidnight = nextMidnight.AddMinutes(30);
        }

        return TimeZoneInfo.ConvertTimeToUtc(nextMidnight, zone);
    }
}
