using KorTrainer.Application.Mistakes;
using KorTrainer.Application.Reading;
using KorTrainer.Application.Typing;
using KorTrainer.Application.Sentences;
using KorTrainer.Application.Hanja;
using KorTrainer.Application.Activity;
using KorTrainer.Application.Common;
using KorTrainer.Application.Grammar;
using KorTrainer.Application.Listening;
using KorTrainer.Application.Numbers;
using KorTrainer.Application.Plan;
using KorTrainer.Application.Stats;
using KorTrainer.Application.Topik;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Progress;
using KorTrainer.Application.Settings;
using KorTrainer.Application.Today;
using KorTrainer.Application.Vocabulary;
using KorTrainer.Domain.Srs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KorTrainer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // TryAdd lets tests substitute a fake clock or a seeded random source.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(Random.Shared);
        services.AddSingleton(new SrsScheduler(SrsOptions.Default));
        services.AddSingleton<StudyClock>();

        services.AddScoped<SettingsService>();
        services.AddScoped<VocabularyService>();
        services.AddScoped<ActivityService>();
        services.AddScoped<HangulService>();
        services.AddScoped<GrammarService>();
        services.AddSingleton<GrammarClozeCache>();
        services.AddScoped<NumbersService>();
        services.AddScoped<ListeningService>();
        services.AddScoped<TopikService>();
        services.AddScoped<StatsService>();
        services.AddScoped<StepEvaluator>();
        services.AddScoped<PlanCursor>();
        services.AddScoped<TodayService>();
        services.AddScoped<ReminderService>();
        services.AddScoped<HanjaService>();
        services.AddScoped<WordDetailsService>();
        services.AddScoped<SentenceService>();
        services.AddScoped<TypingService>();
        services.AddScoped<ReadingService>();
        services.AddScoped<TopikReadingService>();
        services.AddScoped<MistakeService>();
        services.AddScoped<ProgressTransferService>();
        return services;
    }
}
