using KorTrainer.Application.Activity;
using KorTrainer.Application.Common;
using KorTrainer.Application.Grammar;
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
        services.AddScoped<TodayService>();
        services.AddScoped<ProgressTransferService>();
        return services;
    }
}
