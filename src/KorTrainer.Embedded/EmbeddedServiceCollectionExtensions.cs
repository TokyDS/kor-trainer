using KorTrainer.Application;
using KorTrainer.Infrastructure;
using KorTrainer.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KorTrainer.Embedded;

public static class EmbeddedServiceCollectionExtensions
{
    /// <summary>
    /// Registers the application and infrastructure layers with a local SQLite file and serves them to the
    /// pages through <see cref="LocalKorTrainerApi"/>. Recordings are downloaded on demand only (no prefetch).
    /// </summary>
    /// <param name="dataDirectory">Private app storage for the database.</param>
    /// <param name="cacheDirectory">Storage the system may clear, for downloaded recordings.</param>
    public static IServiceCollection AddEmbeddedTrainer(this IServiceCollection services, string dataDirectory, string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{Infrastructure.DependencyInjection.ConnectionStringName}"] = $"Data Source={Path.Combine(dataDirectory, "kortrainer.db")}",
                ["Audio:CacheDirectory"] = Path.Combine(cacheDirectory, "audio"),
                ["Audio:Prefetch"] = "false",
            })
            .Build();

        // Options such as Audio bind from IConfiguration in DI, which a web host would otherwise provide.
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton<LocalKorTrainerApi>();
        services.AddSingleton<IKorTrainerApi>(sp => sp.GetRequiredService<LocalKorTrainerApi>());
        return services;
    }
}
