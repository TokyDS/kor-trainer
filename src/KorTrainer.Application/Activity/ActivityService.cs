using KorTrainer.Application.Common;
using KorTrainer.Application.Mistakes;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Activity;

/// <summary>Records what the learner did; the source for task progress, streaks and statistics.</summary>
public sealed class ActivityService(IAppDbContext db, SettingsService settingsService, StudyClock clock)
{
    public const int MaxBatchSize = 200;
    public const int MaxResponseMs = 10 * 60 * 1000;
    public const int MaxSessionSeconds = 4 * 60 * 60;
    public const int MaxKeyLength = 128;

    public async Task<int> RecordAttemptsAsync(IReadOnlyList<AttemptDto> attempts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        var validator = new Validator()
            .Require(attempts.Count is >= 1 and <= MaxBatchSize, nameof(attempts), $"Пакет ответов: от 1 до {MaxBatchSize}.");
        for (int i = 0; i < attempts.Count; i++)
        {
            var a = attempts[i];
            if (a is null)
            {
                validator.Require(false, $"attempts[{i}]", "Пустой ответ.");
                continue;
            }

            validator
                .Require(Enum.IsDefined(a.Module), $"attempts[{i}].module", "Неизвестный модуль.")
                .Require(IsKey(a.ExerciseType), $"attempts[{i}].exerciseType", "Некорректный тип упражнения.")
                .Require(IsKey(a.ItemKey), $"attempts[{i}].itemKey", "Некорректный ключ задания.")
                .Require(a.ResponseMs >= 0, $"attempts[{i}].responseMs", "Время ответа не может быть отрицательным.")
                .Require(a.Exercise is null || MistakeService.Serialize(a.Exercise).Length <= MistakeLimits.MaxPayloadLength, $"attempts[{i}].exercise", "Слишком большое упражнение.");
        }

        validator.ThrowIfInvalid();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var now = clock.UtcNow;
        var today = StudyClock.LocalDate(settings, now);
        db.ExerciseAttempts.AddRange(attempts.Select(a => new ExerciseAttempt
        {
            Module = a.Module,
            ExerciseType = a.ExerciseType,
            ItemKey = a.ItemKey,
            IsCorrect = a.IsCorrect,
            ResponseMs = Math.Min(a.ResponseMs, MaxResponseMs),
            AnsweredUtc = now,
            LocalDate = today,
        }));
        await TrackMistakesAsync(attempts, today, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return attempts.Count;
    }

    /// <summary>A wrong answer opens (or reopens) a mistake; any later answer to the same item moves it on its schedule.</summary>
    private async Task TrackMistakesAsync(IReadOnlyList<AttemptDto> attempts, DateOnly today, CancellationToken cancellationToken)
    {
        var keys = attempts.Select(a => a.ItemKey).Distinct().ToList();
        var known = await db.Mistakes.Where(m => keys.Contains(m.ItemKey)).ToListAsync(cancellationToken);
        foreach (var attempt in attempts)
        {
            var mistake = known.FirstOrDefault(m => m.Module == attempt.Module && m.ItemKey == attempt.ItemKey);
            if (mistake is null)
            {
                if (attempt.IsCorrect || attempt.Exercise is null)
                {
                    continue;
                }

                var state = MistakeSchedule.Missed(today);
                mistake = new Mistake
                {
                    Module = attempt.Module,
                    ExerciseType = attempt.ExerciseType,
                    ItemKey = attempt.ItemKey,
                    Payload = MistakeService.Serialize(attempt.Exercise),
                    CreatedDate = today,
                    DueDate = state.Due,
                    Lapses = 1,
                };
                db.Mistakes.Add(mistake);
                known.Add(mistake);
                continue;
            }

            var next = MistakeSchedule.Answered(new MistakeState(mistake.Box, mistake.DueDate, mistake.ResolvedDate), attempt.IsCorrect, today);
            if (!attempt.IsCorrect)
            {
                mistake.Lapses++;
                if (attempt.Exercise is not null)
                {
                    mistake.Payload = MistakeService.Serialize(attempt.Exercise);
                }
            }

            (mistake.Box, mistake.DueDate, mistake.ResolvedDate) = (next.Box, next.Due, next.Resolved);
        }
    }

    public async Task RecordSessionAsync(SessionDto session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var now = clock.UtcNow;
        var started = UtcTime.Normalize(session.StartedUtc);
        new Validator()
            .Require(Enum.IsDefined(session.Module), nameof(session.Module), "Неизвестный модуль.")
            .Require(session.DurationSeconds is >= 1 and <= MaxSessionSeconds, nameof(session.DurationSeconds), "Длительность занятия: от 1 секунды до 4 часов.")
            .Require(started <= now.AddMinutes(5) && started >= now.AddDays(-2), nameof(session.StartedUtc), "Время начала занятия вне допустимого диапазона.")
            .ThrowIfInvalid();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        db.StudySessions.Add(new StudySession
        {
            Module = session.Module,
            StartedUtc = started,
            DurationSeconds = session.DurationSeconds,
            LocalDate = StudyClock.LocalDate(settings, started),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordReadingTrialAsync(ReadingTrialDto trial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trial);
        new Validator()
            .Require(!string.IsNullOrWhiteSpace(trial.Text) && trial.Text.Length <= MaxKeyLength, nameof(trial.Text), "Некорректный текст.")
            .Require(trial.SyllableCount is >= 1 and <= MaxKeyLength, nameof(trial.SyllableCount), "Некорректное число слогов.")
            .Require(trial.ElapsedMs is >= 1 and <= MaxResponseMs, nameof(trial.ElapsedMs), "Некорректное время чтения.")
            .ThrowIfInvalid();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var now = clock.UtcNow;
        db.ReadingTrials.Add(new ReadingTrial
        {
            Text = trial.Text,
            SyllableCount = trial.SyllableCount,
            ElapsedMs = trial.ElapsedMs,
            ReadCorrectly = trial.ReadCorrectly,
            CompletedUtc = now,
            LocalDate = StudyClock.LocalDate(settings, now),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsKey(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= MaxKeyLength;
}
