using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Topik;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Topik;

/// <summary>TOPIK II writing practice: task guide and timed drafts.</summary>
public sealed class TopikService(IAppDbContext db, SettingsService settingsService, StudyClock clock)
{
    public const int MaxDraftLength = 4000;
    public const int MaxDraftSeconds = 3 * 60 * 60;
    public const int DraftsPageSize = 20;

    public static WritingGuideDto GetGuide() => new(
        WritingTasks.PastPapersUrl,
        WritingTasks.SectionMinutes,
        WritingTasks.All.Select(t => new WritingTaskDto(
            t.Number,
            t.Title,
            t.Format,
            t.RecommendedMinutes,
            t.MinChars,
            t.MaxChars,
            t.Tips,
            t.Template.Select(p => new TemplatePhraseDto(p.Part, p.Korean, p.Russian)).ToList())).ToList());

    public async Task<WritingDraftDto> SaveDraftAsync(SaveDraftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var started = UtcTime.Normalize(request.StartedUtc);
        new Validator()
            .Require(WritingTasks.Find(request.TaskNumber) is not null, nameof(request.TaskNumber), "Номер задания: 51–54.")
            .Require(request.Text is { Length: <= MaxDraftLength }, nameof(request.Text), $"Текст не длиннее {MaxDraftLength} символов.")
            .Require(request.DurationSeconds is >= 0 and <= MaxDraftSeconds, nameof(request.DurationSeconds), "Некорректная длительность.")
            .Require(started <= now.AddMinutes(5) && started >= now.AddDays(-2), nameof(request.StartedUtc), "Время начала вне допустимого диапазона.")
            .ThrowIfInvalid();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var draft = new WritingDraft
        {
            TaskNumber = request.TaskNumber,
            StartedUtc = started,
            DurationSeconds = request.DurationSeconds,
            Text = request.Text,
            LocalDate = StudyClock.LocalDate(settings, started),
        };
        db.WritingDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(draft);
    }

    public async Task<IReadOnlyList<WritingDraftDto>> ListDraftsAsync(int? taskNumber, CancellationToken cancellationToken)
    {
        var query = db.WritingDrafts.AsNoTracking();
        if (taskNumber is { } number)
        {
            query = query.Where(d => d.TaskNumber == number);
        }

        var drafts = await query.OrderByDescending(d => d.Id).Take(DraftsPageSize).ToListAsync(cancellationToken);
        return drafts.Select(ToDto).ToList();
    }

    private static WritingDraftDto ToDto(WritingDraft d) =>
        new(d.Id, d.TaskNumber, d.StartedUtc, d.DurationSeconds, WritingTasks.CountCharacters(d.Text), d.Text);
}
