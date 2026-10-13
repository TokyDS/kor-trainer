using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Hangul;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Vocabulary;

public sealed class VocabularyService(IAppDbContext db, SettingsService settingsService, StudyClock clock, SrsScheduler scheduler)
{
    public const int DefaultQueueSize = 50;
    public const int MaxQueueSize = 200;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;
    public const int MaxReviewDurationMs = 10 * 60 * 1000;

    /// <summary>Learning cards due this soon are shown right away instead of making the learner wait.</summary>
    public static readonly TimeSpan LearnAhead = TimeSpan.FromMinutes(20);

    public async Task<VocabularyQueueDto> GetQueueAsync(int limit, CancellationToken cancellationToken)
    {
        new Validator().Require(limit is >= 1 and <= MaxQueueSize, nameof(limit), $"Размер очереди: от 1 до {MaxQueueSize}.").ThrowIfInvalid();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var now = clock.UtcNow;
        var today = clock.Today(settings);
        var endOfDay = StudyClock.EndOfLocalDayUtc(settings, today);
        var unlock = new StudyPlan(settings.StartDate, settings.NewWordsPerDay).WordUnlockOn(today);

        int reviewsToday = await db.ReviewLogs.CountAsync(r => r.LocalDate == today && r.PhaseBefore == CardPhase.Review, cancellationToken);
        int introducedToday = await CountIntroducedAsync(today, cancellationToken);
        int reviewAllowance = Math.Max(0, settings.MaxReviewsPerDay - reviewsToday);
        int newRemaining = Math.Max(0, settings.NewWordsPerDay - introducedToday);
        var learnAheadUntil = now + LearnAhead;

        var learning = await db.WordProgress
            .Include(p => p.Word)
            .Where(p => (p.Phase == CardPhase.Learning || p.Phase == CardPhase.Relearning) && p.DueUtc <= learnAheadUntil)
            .OrderBy(p => p.DueUtc)
            .ToListAsync(cancellationToken);

        var reviews = await db.WordProgress
            .Include(p => p.Word)
            .Where(p => p.Phase == CardPhase.Review && p.DueUtc < endOfDay)
            .OrderBy(p => p.DueUtc)
            .Take(reviewAllowance)
            .ToListAsync(cancellationToken);

        var newWords = await db.Words
            .Where(w => w.Progress == null)
            .Unlocked(unlock)
            .InIntroductionOrder()
            .Take(newRemaining)
            .ToListAsync(cancellationToken);

        var dueLearning = learning.Where(p => p.DueUtc <= now).ToList();
        var aheadLearning = learning.Where(p => p.DueUtc > now).ToList();

        var cards = dueLearning.Select(p => ToCard(p.Word!, p.ToCardState(), now))
            .Concat(reviews.Select(p => ToCard(p.Word!, p.ToCardState(), now)))
            .Concat(newWords.Select(w => ToCard(w, CardState.New(now), now)))
            .Concat(aheadLearning.Select(p => ToCard(p.Word!, p.ToCardState(), now)))
            .Take(limit)
            .ToList();

        DateTime? nextLearningDue = cards.Count > 0
            ? null
            : await db.WordProgress
                .Where(p => p.Phase == CardPhase.Learning || p.Phase == CardPhase.Relearning)
                .OrderBy(p => p.DueUtc)
                .Select(p => (DateTime?)p.DueUtc)
                .FirstOrDefaultAsync(cancellationToken);

        return new VocabularyQueueDto(cards, dueLearning.Count + reviews.Count, Math.Min(newRemaining, newWords.Count), nextLearningDue);
    }

    public async Task<ReviewResultDto> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        new Validator()
            .Require(Enum.IsDefined(request.Grade), nameof(request.Grade), "Неизвестная оценка.")
            .Require(request.DurationMs >= 0, nameof(request.DurationMs), "Длительность не может быть отрицательной.")
            .ThrowIfInvalid();

        var word = await db.Words.Include(w => w.Progress).SingleOrDefaultAsync(w => w.Id == request.WordId, cancellationToken)
            ?? throw new NotFoundException($"Слово {request.WordId} не найдено.");
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var now = clock.UtcNow;

        var before = word.Progress?.ToCardState() ?? CardState.New(now);
        var after = scheduler.Schedule(before, request.Grade, now);

        var progress = word.Progress;
        if (progress is null)
        {
            progress = new WordProgress { WordId = word.Id, IntroducedUtc = now };
            db.WordProgress.Add(progress);
        }

        progress.Apply(after, now);
        db.ReviewLogs.Add(new ReviewLog
        {
            WordId = word.Id,
            ReviewedUtc = now,
            LocalDate = StudyClock.LocalDate(settings, now),
            Grade = request.Grade,
            PhaseBefore = before.Phase,
            IntervalBefore = before.IntervalDays,
            IntervalAfter = after.IntervalDays,
            DurationMs = Math.Min(request.DurationMs, MaxReviewDurationMs),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two tabs answered the same new card at once: the first answer wins.
            throw new ConflictException("Эта карточка уже была оценена. Обновите очередь.", ex);
        }

        return new ReviewResultDto(word.Id, after.Phase, after.DueUtc, after.IntervalDays);
    }

    public async Task<VocabularySummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var now = clock.UtcNow;
        var today = clock.Today(settings);
        var endOfDay = StudyClock.EndOfLocalDayUtc(settings, today);
        var unlock = new StudyPlan(settings.StartDate, settings.NewWordsPerDay).WordUnlockOn(today);

        var phases = await db.WordProgress
            .Select(p => new { p.Phase, p.IntervalDays, p.DueUtc })
            .ToListAsync(cancellationToken);

        int learning = phases.Count(p => p.Phase is CardPhase.Learning or CardPhase.Relearning);
        int young = phases.Count(p => p.Phase == CardPhase.Review && p.IntervalDays < CardState.MatureIntervalDays);
        int mature = phases.Count(p => p.Phase == CardPhase.Review && p.IntervalDays >= CardState.MatureIntervalDays);
        int dueNow = phases.Count(p =>
            (p.Phase == CardPhase.Review && p.DueUtc < endOfDay) ||
            (p.Phase is CardPhase.Learning or CardPhase.Relearning && p.DueUtc <= now + LearnAhead));

        return new VocabularySummaryDto(
            Total: await db.Words.CountAsync(cancellationToken),
            Unlocked: await db.Words.Unlocked(unlock).CountAsync(cancellationToken),
            Unseen: await db.Words.Where(w => w.Progress == null).Unlocked(unlock).CountAsync(cancellationToken),
            Learning: learning,
            Young: young,
            Mature: mature,
            DueNow: dueNow,
            NewToday: await CountIntroducedAsync(today, cancellationToken),
            ReviewsToday: await db.ReviewLogs.CountAsync(r => r.LocalDate == today, cancellationToken));
    }

    public async Task<WordPageDto> ListWordsAsync(
        string? topic,
        PlanStage? stage,
        WordStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        new Validator()
            .Require(page is >= 1 and <= MaxPage, nameof(page), $"Номер страницы: от 1 до {MaxPage}.")
            .Require(pageSize is >= 1 and <= MaxPageSize, nameof(pageSize), $"Размер страницы: от 1 до {MaxPageSize}.")
            .Require(search is null || search.Length <= 50, nameof(search), "Слишком длинный запрос.")
            .ThrowIfInvalid();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var unlock = new StudyPlan(settings.StartDate, settings.NewWordsPerDay).WordUnlockOn(clock.Today(settings));

        var query = db.Words.Include(w => w.Progress).AsQueryable();
        if (!string.IsNullOrWhiteSpace(topic))
        {
            query = query.Where(w => w.Topic == topic);
        }

        if (stage is { } s)
        {
            query = query.Where(w => w.Stage == s);
        }

        if (status is { } st)
        {
            query = query.WithStatus(st, unlock);
        }

        List<Word> words;
        int total;
        if (string.IsNullOrWhiteSpace(search))
        {
            total = await query.CountAsync(cancellationToken);
            words = await query.InIntroductionOrder().Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        }
        else
        {
            // SQLite's LIKE folds case for ASCII only; the deck is small, so search Cyrillic in memory.
            string term = search.Trim();
            var matches = (await query.InIntroductionOrder().ToListAsync(cancellationToken))
                .Where(w => w.Korean.Contains(term, StringComparison.OrdinalIgnoreCase) || w.Russian.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
            total = matches.Count;
            words = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        var items = words
            .Select(w => new WordDto(w.Id, w.Korean, w.Russian, w.Topic, w.PartOfSpeech, w.Stage, w.LessonDay, WordQueries.StatusOf(w, unlock), w.Progress?.DueUtc))
            .ToList();
        return new WordPageDto(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<TopicDto>> GetTopicsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Words
            .GroupBy(w => w.Topic)
            .Select(g => new
            {
                Topic = g.Key,
                Total = g.Count(),
                Learned = g.Count(w => w.Progress != null && w.Progress.Phase == CardPhase.Review),
                FirstStage = g.Min(w => w.Stage),
                FirstRank = g.Min(w => w.Rank),
            })
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(r => r.FirstStage)
            .ThenBy(r => r.FirstRank)
            .Select(r => new TopicDto(r.Topic, r.Total, r.Learned))
            .ToList();
    }

    private Task<int> CountIntroducedAsync(DateOnly today, CancellationToken cancellationToken) =>
        db.ReviewLogs.CountAsync(r => r.LocalDate == today && r.PhaseBefore == CardPhase.New, cancellationToken);

    private CardDto ToCard(Word word, CardState state, DateTime now)
    {
        var previews = scheduler.Preview(state, now)
            .OrderBy(p => p.Key)
            .Select(p => new GradePreviewDto(p.Key, IntervalFormatter.Label(p.Key), IntervalFormatter.Format(p.Value)))
            .ToList();
        return new CardDto(
            word.Id,
            word.Korean,
            word.Russian,
            word.Topic,
            word.PartOfSpeech,
            state.Phase,
            Pronunciation.Pronounce(word.Korean).Text,
            state.DueUtc,
            previews);
    }
}
