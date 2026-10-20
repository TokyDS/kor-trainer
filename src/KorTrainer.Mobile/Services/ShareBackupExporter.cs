using KorTrainer.Embedded;
using KorTrainer.UI.Services;

namespace KorTrainer.Mobile.Services;

/// <summary>Writes the backup to the app cache and opens the share sheet (Files, Drive, Telegram…).</summary>
public sealed class ShareBackupExporter(LocalKorTrainerApi api) : IBackupExporter
{
    public async Task ExportAsync()
    {
        byte[] json = await api.ExportProgressAsync();
        string path = Path.Combine(FileSystem.CacheDirectory, $"kortrainer-progress-{DateTime.Now:yyyy-MM-dd}.json");
        await File.WriteAllBytesAsync(path, json);
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Резервная копия KorTrainer",
            File = new ShareFile(path, "application/json"),
        });
    }
}
