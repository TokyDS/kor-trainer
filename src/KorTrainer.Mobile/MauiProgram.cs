using KorTrainer.Application.Common;
using KorTrainer.Embedded;
using KorTrainer.Mobile.Services;
using KorTrainer.UI.Services;
using Microsoft.Extensions.Logging;

namespace KorTrainer.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        // Database in private app storage; recordings in the cache, which Android may clear when space runs low.
        builder.Services.AddEmbeddedTrainer(FileSystem.AppDataDirectory, FileSystem.CacheDirectory);
        builder.Services.AddScoped<BrowserInterop>();
        // Registered after AddEmbeddedTrainer, so the APK's bundled speech replaces the directory reader.
        builder.Services.AddSingleton<ISynthesizedSpeech, PackagedSynthesizedSpeech>();
        builder.Services.AddScoped<ISpeechOutput, AndroidSpeechOutput>();
        builder.Services.AddSingleton<ISpeechInput, AndroidSpeechInput>();
        builder.Services.AddSingleton<IBackupExporter, ShareBackupExporter>();
        builder.Services.AddScoped<AppState>();
        builder.Services.AddSingleton<StudyReminders>();

        return builder.Build();
    }
}
