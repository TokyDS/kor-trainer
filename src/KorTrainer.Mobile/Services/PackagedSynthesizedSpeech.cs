using KorTrainer.Application.Common;
using KorTrainer.Infrastructure.Audio;

namespace KorTrainer.Mobile.Services;

/// <summary>Neural speech bundled in the APK (MauiAsset "tts/…"), so it plays offline.</summary>
public sealed class PackagedSynthesizedSpeech : ISynthesizedSpeech
{
    private const string Folder = "tts";

    private readonly Lazy<SynthesizedSpeechDirectory.Manifest> _manifest = new(Load);

    public double GeneratedSpeed => _manifest.Value.Speed;

    public bool Has(string text) => _manifest.Value.Items.ContainsKey(text.Trim());

    public async Task<AudioFile?> OpenAsync(string text, CancellationToken cancellationToken)
    {
        if (!_manifest.Value.Items.TryGetValue(text.Trim(), out string? key))
        {
            return null;
        }

        // Asset streams cannot seek; copy the small file so the page can read it in one go.
        await using var asset = await FileSystem.OpenAppPackageFileAsync($"{Folder}/{key}.opus");
        var buffer = new MemoryStream();
        await asset.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return new AudioFile(buffer, SynthesizedSpeechDirectory.ContentType);
    }

    private static SynthesizedSpeechDirectory.Manifest Load()
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync($"{Folder}/manifest.json").GetAwaiter().GetResult();
            return SynthesizedSpeechDirectory.Parse(stream);
        }
        catch (FileNotFoundException)
        {
            // Built without generated speech: the system voice is used.
            return new SynthesizedSpeechDirectory.Manifest(1.0, new Dictionary<string, string>());
        }
    }
}
