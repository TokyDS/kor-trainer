using System.Net;
using AwesomeAssertions;
using KorTrainer.Infrastructure.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KorTrainer.Application.Tests;

public sealed class LinguaLibreAudioLibraryTests : IDisposable
{
    private static readonly string[] OpenLicenses = ["CC0", "CC BY", "CC BY-SA", "Public domain"];
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"kortrainer-audio-{Guid.NewGuid():N}");
    private readonly FakeHandler _handler = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Manifest_ListsOpenlyLicensedHangulWordsFromWikimedia()
    {
        var library = CreateLibrary();

        library.Words.Count.Should().BeGreaterThan(900);
        library.Words.Should().Contain(["가다", "까다", "바다", "파다"]);
        library.Words.Should().OnlyContain(w => w.All(c => (c >= '가' && c <= '힣') || c == ' '));
        library.Credits.Should().OnlyContain(c => OpenLicenses.Any(l => c.License.StartsWith(l, StringComparison.Ordinal)));
        library.Credits.Should().OnlyContain(c => c.Source.StartsWith("https://commons.wikimedia.org/wiki/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenAsync_DownloadsOnceThenServesFromCache()
    {
        var library = CreateLibrary();

        await using (var first = (await library.OpenAsync("가다", Ct))!.Content)
        {
            first.Length.Should().Be(FakeHandler.Payload.Length);
        }

        var second = await library.OpenAsync("가다", Ct);
        await second!.Content.DisposeAsync();

        _handler.Calls.Should().Be(1);
        _handler.LastHost.Should().Be("upload.wikimedia.org");
        library.IsCached("가다").Should().BeTrue();
    }

    [Fact]
    public async Task OpenAsync_UnknownWord_DoesNotTouchTheNetwork()
    {
        var library = CreateLibrary();

        (await library.OpenAsync("없는단어", Ct)).Should().BeNull();
        _handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task OpenAsync_RejectsNonAudioResponses()
    {
        _handler.ContentType = "text/html";
        var library = CreateLibrary();

        (await library.OpenAsync("가다", Ct)).Should().BeNull();
        library.IsCached("가다").Should().BeFalse();
    }

    [Fact]
    public async Task OpenAsync_ReturnsNullWhenWikimediaIsUnreachable()
    {
        _handler.Fail = true;
        var library = CreateLibrary();

        (await library.OpenAsync("가다", Ct)).Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private LinguaLibreAudioLibrary CreateLibrary() => new(
        new SingleClientFactory(_handler),
        Options.Create(new AudioOptions { CacheDirectory = _cacheDirectory, Prefetch = false }),
        NullLogger<LinguaLibreAudioLibrary>.Instance);

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public static readonly byte[] Payload = [0x49, 0x44, 0x33, 4, 0, 0];

        public int Calls { get; private set; }

        public string? LastHost { get; private set; }

        public string ContentType { get; set; } = "audio/mpeg";

        public bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastHost = request.RequestUri?.Host;
            if (Fail)
            {
                throw new HttpRequestException("offline");
            }

            var content = new ByteArrayContent(Payload);
            content.Headers.ContentType = new(ContentType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
