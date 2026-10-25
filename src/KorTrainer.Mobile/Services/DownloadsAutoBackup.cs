using System.Runtime.Versioning;
using Android.Content;
using Android.Provider;
using KorTrainer.Embedded;
using KorTrainer.UI.Services;
using Microsoft.Extensions.Logging;
using AndroidApp = Android.App.Application;
using AndroidEnvironment = Android.OS.Environment;

namespace KorTrainer.Mobile.Services;

/// <summary>
/// Once a week, when the learner leaves the app, a copy of the progress goes to Downloads/KorTrainer: outside the
/// app's private storage, so it survives a reinstall or cleared app data and can be imported back in the settings.
/// One copy per day and the four newest days stay; only copies this app wrote are ever removed. Android 8–9 has no
/// shared Downloads for apps without a storage permission, so there the copies stay in the app's own external
/// folder, which is removed with the app.
/// </summary>
public sealed class DownloadsAutoBackup(LocalKorTrainerApi api, ILogger<DownloadsAutoBackup> logger) : IAutoBackup
{
    private const string LastSavedKey = "autobackup.last-utc";
    private const string FilePrefix = "kortrainer-progress-";
    private const string SubFolder = "KorTrainer";
    private const int KeptCopies = 4;
    private static readonly TimeSpan Interval = TimeSpan.FromDays(7);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private static string Folder => OperatingSystem.IsAndroidVersionAtLeast(29)
        ? $"Загрузки → {SubFolder}"
        : $"Android/data/{AndroidApp.Context.PackageName}/files/{AndroidEnvironment.DirectoryDownloads}/{SubFolder} (удаляется вместе с приложением)";

    public Task<AutoBackupStatus?> GetStatusAsync() => Task.FromResult<AutoBackupStatus?>(Status());

    public Task<AutoBackupStatus> SaveNowAsync() => SaveAsync(force: true);

    /// <summary>Writes a copy when the last one is a week old. Never throws: a failed copy is tried again next time.</summary>
    public async Task SaveIfDueAsync()
    {
        try
        {
            await SaveAsync(force: false);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Automatic backup failed");
        }
    }

    private async Task<AutoBackupStatus> SaveAsync(bool force)
    {
        await _gate.WaitAsync();
        try
        {
            // Checked inside the gate: leaving the app twice in a row writes one copy.
            if (!force && !IsDue())
            {
                return Status();
            }

            try
            {
                byte[] json = await api.ExportProgressAsync();
                string name = $"{FilePrefix}{DateTime.Now:yyyy-MM-dd}.json";
                await Task.Run(() =>
                {
                    if (OperatingSystem.IsAndroidVersionAtLeast(29))
                    {
                        WriteToDownloads(name, json);
                    }
                    else
                    {
                        WriteToAppFolder(name, json);
                    }
                });
            }
            catch (Exception ex) when (ex is not IOException and not OperationCanceledException)
            {
                // Export (database) and storage (Java, file system) failures all mean the same to the learner.
                throw new IOException($"Не удалось сохранить копию: {ex.Message}", ex);
            }

            Preferences.Default.Set(LastSavedKey, DateTime.UtcNow);
            await Task.Run(Prune);
            return Status();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>A clock set back makes the last copy look newer than now: then a copy is due as well.</summary>
    private static bool IsDue() =>
        LastSavedUtc() is not { } last || last > DateTime.UtcNow || DateTime.UtcNow - last >= Interval;

    private static AutoBackupStatus Status() => new(LastSavedUtc(), Folder);

    private static DateTime? LastSavedUtc()
    {
        var saved = Preferences.Default.Get(LastSavedKey, DateTime.MinValue);
        return saved == DateTime.MinValue ? null : DateTime.SpecifyKind(saved, DateTimeKind.Utc);
    }

    /// <summary>The new copy is written in full before any old one goes; a half-written file is removed.</summary>
    [SupportedOSPlatform("android29.0")]
    private static void WriteToDownloads(string name, byte[] json)
    {
        var resolver = AndroidApp.Context.ContentResolver ?? throw new IOException("Хранилище недоступно.");
        var collection = MediaStore.Downloads.ExternalContentUri ?? throw new IOException("Папка «Загрузки» недоступна.");

        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, name);
        values.Put(MediaStore.IMediaColumns.MimeType, "application/json");
        values.Put(MediaStore.IMediaColumns.RelativePath, DownloadsPath);
        values.Put(MediaStore.IMediaColumns.IsPending, 1);
        var uri = resolver.Insert(collection, values) ?? throw new IOException("Не удалось создать файл в «Загрузках».");
        try
        {
            using (var stream = resolver.OpenOutputStream(uri) ?? throw new IOException("Не удалось открыть файл в «Загрузках»."))
            {
                stream.Write(json);
            }

            values.Clear();
            values.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, values, null, null);
        }
        catch
        {
            resolver.Delete(uri, null, null);
            throw;
        }
    }

    private static string DownloadsPath => $"{AndroidEnvironment.DirectoryDownloads}/{SubFolder}/";

    private static void WriteToAppFolder(string name, byte[] json)
    {
        string path = Path.Combine(AppFolder(), name);
        File.WriteAllBytes(path + ".tmp", json);
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private static string AppFolder()
    {
        var root = AndroidApp.Context.GetExternalFilesDir(AndroidEnvironment.DirectoryDownloads)
            ?? throw new IOException("Внешнее хранилище недоступно.");
        string folder = Path.Combine(root.AbsolutePath, SubFolder);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Keeps the newest copy of each of the last <see cref="KeptCopies"/> days. Best effort: the copy is already
    /// written, so a failure here only leaves an extra old file.
    /// </summary>
    private void Prune()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                PruneDownloads();
            }
            else
            {
                foreach (var old in Directory.GetFiles(AppFolder(), FilePrefix + "*.json").OrderDescending(StringComparer.Ordinal).Skip(KeptCopies))
                {
                    File.Delete(old);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Java.Lang.Exception)
        {
            logger.LogWarning(ex, "Old backups were not removed");
        }
    }

    /// <summary>A second copy the same day is saved as «… (1).json» by Android; the older one of the day goes.</summary>
    [SupportedOSPlatform("android29.0")]
    private static void PruneDownloads()
    {
        var resolver = AndroidApp.Context.ContentResolver ?? throw new IOException("Хранилище недоступно.");
        var collection = MediaStore.Downloads.ExternalContentUri ?? throw new IOException("Папка «Загрузки» недоступна.");
        var copies = OwnCopies(resolver, collection);
        var kept = copies
            .GroupBy(c => c.Name.Length >= FilePrefix.Length + 10 ? c.Name[..(FilePrefix.Length + 10)] : c.Name, StringComparer.Ordinal)
            .Take(KeptCopies)
            .Select(g => g.First().Id)
            .ToHashSet();
        foreach (var (id, _) in copies.Where(c => !kept.Contains(c.Id)))
        {
            resolver.Delete(ContentUris.WithAppendedId(collection, id), null, null);
        }
    }

    /// <summary>Copies in the folder that this app wrote, newest first: other apps' files are not visible to it.</summary>
    [SupportedOSPlatform("android29.0")]
    private static List<(long Id, string Name)> OwnCopies(ContentResolver resolver, Android.Net.Uri collection)
    {
        var copies = new List<(long, string)>();
        using var cursor = resolver.Query(
            collection,
            [IBaseColumns.Id, MediaStore.IMediaColumns.DisplayName],
            $"{MediaStore.IMediaColumns.RelativePath} = ? AND {MediaStore.IMediaColumns.DisplayName} LIKE ?",
            [DownloadsPath, FilePrefix + "%"],
            $"{MediaStore.IMediaColumns.DateAdded} DESC, {IBaseColumns.Id} DESC");
        while (cursor is not null && cursor.MoveToNext())
        {
            copies.Add((cursor.GetLong(0), cursor.GetString(1) ?? string.Empty));
        }

        return copies;
    }
}
