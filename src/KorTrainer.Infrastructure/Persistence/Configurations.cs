using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorTrainer.Infrastructure.Persistence;

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TimeZoneId).HasMaxLength(64);
    }
}

internal sealed class WordConfiguration : IEntityTypeConfiguration<Word>
{
    public void Configure(EntityTypeBuilder<Word> builder)
    {
        builder.HasKey(w => w.Id);
        builder.HasIndex(w => w.DeckKey).IsUnique();
        builder.Property(w => w.Hanja).HasMaxLength(16);
        builder.HasIndex(w => new { w.Stage, w.LessonDay, w.Rank });
        builder.HasIndex(w => w.Topic);
        builder.Property(w => w.DeckKey).HasMaxLength(64);
        builder.Property(w => w.Korean).HasMaxLength(64);
        builder.Property(w => w.Russian).HasMaxLength(256);
        builder.Property(w => w.Topic).HasMaxLength(32);
        builder.HasOne(w => w.Progress)
            .WithOne(p => p.Word)
            .HasForeignKey<WordProgress>(p => p.WordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WordProgressConfiguration : IEntityTypeConfiguration<WordProgress>
{
    public void Configure(EntityTypeBuilder<WordProgress> builder)
    {
        builder.HasKey(p => p.WordId);
        builder.Property(p => p.WordId).ValueGeneratedNever();
        builder.HasIndex(p => new { p.Phase, p.DueUtc });
    }
}

internal sealed class ReviewLogConfiguration : IEntityTypeConfiguration<ReviewLog>
{
    public void Configure(EntityTypeBuilder<ReviewLog> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.LocalDate);
        builder.HasIndex(r => r.WordId);
        builder.HasOne<Word>().WithMany().HasForeignKey(r => r.WordId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExerciseAttemptConfiguration : IEntityTypeConfiguration<ExerciseAttempt>
{
    public void Configure(EntityTypeBuilder<ExerciseAttempt> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.LocalDate, a.Module });
        builder.Property(a => a.ExerciseType).HasMaxLength(128);
        builder.Property(a => a.ItemKey).HasMaxLength(128);
    }
}

internal sealed class MistakeConfiguration : IEntityTypeConfiguration<Mistake>
{
    public void Configure(EntityTypeBuilder<Mistake> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => new { m.Module, m.ItemKey }).IsUnique();
        builder.HasIndex(m => new { m.ResolvedDate, m.DueDate });
        builder.Property(m => m.ExerciseType).HasMaxLength(128);
        builder.Property(m => m.ItemKey).HasMaxLength(128);
        builder.Property(m => m.Payload).HasMaxLength(MistakeLimits.MaxPayloadLength);
    }
}

internal sealed class ReadingTrialConfiguration : IEntityTypeConfiguration<ReadingTrial>
{
    public void Configure(EntityTypeBuilder<ReadingTrial> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.LocalDate);
        builder.Property(r => r.Text).HasMaxLength(128);
    }
}

internal sealed class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    public void Configure(EntityTypeBuilder<StudySession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.LocalDate);
    }
}

internal sealed class WordNoteConfiguration : IEntityTypeConfiguration<WordNote>
{
    public void Configure(EntityTypeBuilder<WordNote> builder)
    {
        builder.HasKey(n => n.WordId);
        builder.Property(n => n.Text).HasMaxLength(WordNoteLimits.MaxLength);
        builder.HasOne(n => n.Word).WithMany().HasForeignKey(n => n.WordId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TranslationFlagConfiguration : IEntityTypeConfiguration<TranslationFlag>
{
    public void Configure(EntityTypeBuilder<TranslationFlag> builder)
    {
        builder.HasKey(f => f.WordId);
        builder.Property(f => f.Russian).HasMaxLength(TranslationFlagLimits.MaxTranslationLength);
        builder.Property(f => f.Note).HasMaxLength(TranslationFlagLimits.MaxNoteLength);
        builder.HasOne(f => f.Word).WithMany().HasForeignKey(f => f.WordId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RuleReviewConfiguration : IEntityTypeConfiguration<RuleReview>
{
    public void Configure(EntityTypeBuilder<RuleReview> builder)
    {
        builder.HasKey(r => r.ExerciseType);
        builder.Property(r => r.ExerciseType).HasMaxLength(128);
    }
}

internal sealed class PlanStepProgressConfiguration : IEntityTypeConfiguration<PlanStepProgress>
{
    public void Configure(EntityTypeBuilder<PlanStepProgress> builder)
    {
        builder.HasKey(p => p.StepKey);
        builder.Property(p => p.StepKey).HasMaxLength(32);
    }
}

internal sealed class StepTaskCompletionConfiguration : IEntityTypeConfiguration<StepTaskCompletion>
{
    public void Configure(EntityTypeBuilder<StepTaskCompletion> builder)
    {
        builder.HasKey(c => new { c.StepKey, c.TaskKey });
        builder.Property(c => c.StepKey).HasMaxLength(32);
        builder.Property(c => c.TaskKey).HasMaxLength(64);
    }
}

internal sealed class WritingDraftConfiguration : IEntityTypeConfiguration<WritingDraft>
{
    public void Configure(EntityTypeBuilder<WritingDraft> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.LocalDate);
        builder.Property(d => d.Text).HasMaxLength(4000);
    }
}
