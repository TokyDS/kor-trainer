using KorTrainer.Application.Common;
using KorTrainer.Infrastructure.Audio;
using KorTrainer.Infrastructure.Content;
using KorTrainer.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KorTrainer.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "KorTrainer";
    private const string DefaultConnectionString = "Data Source=data/kortrainer.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName) ?? DefaultConnectionString;
        EnsureDataDirectory(connectionString);

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DatabaseInitializer>();
        AddAudio(services);
        services.AddSingleton<IHanjaGlossary, EmbeddedHanjaGlossary>();
        services.AddSingleton<ISentenceLibrary, EmbeddedSentenceLibrary>();
        return services;
    }

    private static void AddAudio(IServiceCollection services)
    {
        services.AddOptions<AudioOptions>().BindConfiguration(AudioOptions.SectionName);
        services.AddHttpClient(LinguaLibreAudioLibrary.HttpClientName, (sp, client) =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(sp.GetRequiredService<IOptions<AudioOptions>>().Value.UserAgent);
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddSingleton<LinguaLibreAudioLibrary>();
        services.AddSingleton<IAudioLibrary>(sp => sp.GetRequiredService<LinguaLibreAudioLibrary>());
        services.AddHostedService<AudioPrefetchService>();
    }

    private static void EnsureDataDirectory(string connectionString)
    {
        string dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        string? directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory) && dataSource != ":memory:")
        {
            Directory.CreateDirectory(directory);
        }
    }
}
