using System.Text;
using AwesomeAssertions;
using KorTrainer.Infrastructure.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KorTrainer.Application.Tests;

public sealed class SynthesizedSpeechTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"kortrainer-tts-{Guid.NewGuid():N}");

    [Fact]
    public void Manifest_KeepsOnlyHexKeysAndItsSpeed()
    {
        var manifest = SynthesizedSpeechDirectory.Parse(Json("""{"speed": 0.9, "items": {"학교": "a1b2", " 가요 ": "0f", "x": "../etc/passwd"}}"""));

        manifest.Speed.Should().Be(0.9);
        manifest.Items.Should().BeEquivalentTo(new Dictionary<string, string> { ["학교"] = "a1b2", ["가요"] = "0f" });
    }

    [Fact]
    public void Manifest_WithoutSpeed_IsNormalSpeed() =>
        SynthesizedSpeechDirectory.Parse(Json("""{"items": {}}""")).Speed.Should().Be(1.0);

    [Fact]
    public async Task Directory_OpensGeneratedFiles()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "manifest.json"), """{"speed": 0.9, "items": {"학교": "a1b2"}}""");
        File.WriteAllBytes(Path.Combine(_directory, "a1b2.opus"), [1, 2, 3]);
        var speech = Speech(_directory);

        var file = await speech.OpenAsync("학교 ", TestContext.Current.CancellationToken);

        speech.Has("학교").Should().BeTrue();
        speech.GeneratedSpeed.Should().Be(0.9);
        file!.ContentType.Should().Be("audio/ogg");
        await using var content = file.Content;
        content.Length.Should().Be(3);
        (await speech.OpenAsync("없음", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task MissingDirectory_MeansNoSynthesizedSpeech()
    {
        var speech = Speech(Path.Combine(_directory, "missing"));

        speech.Has("학교").Should().BeFalse();
        (await speech.OpenAsync("학교", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static SynthesizedSpeechDirectory Speech(string directory) =>
        new(Options.Create(new AudioOptions { SynthesizedDirectory = directory }), NullLogger<SynthesizedSpeechDirectory>.Instance);

    private static MemoryStream Json(string json) => new(Encoding.UTF8.GetBytes(json));
}
