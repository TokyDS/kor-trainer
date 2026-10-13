namespace KorTrainer.Application.Common;

internal static class UtcTime
{
    /// <summary>Client timestamps may arrive as Local or Unspecified; treat Unspecified as UTC.</summary>
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
