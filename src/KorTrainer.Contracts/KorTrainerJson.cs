using System.Text.Json;
using System.Text.Json.Serialization;

namespace KorTrainer.Contracts;

/// <summary>JSON settings shared by the HTTP API, its clients and backup files: camelCase, enums as names.</summary>
public static class KorTrainerJson
{
    public static JsonSerializerOptions Options { get; } = Create(indented: false);

    /// <summary>Readable variant for backup files.</summary>
    public static JsonSerializerOptions Indented { get; } = Create(indented: true);

    private static JsonSerializerOptions Create(bool indented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = indented };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
