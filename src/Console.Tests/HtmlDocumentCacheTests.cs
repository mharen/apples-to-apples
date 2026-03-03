using System.Net;
using System.Text.RegularExpressions;
using ApplesToApples.ConsoleApp;

namespace Console.Tests;

public sealed partial class HtmlDocumentCacheTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "html-cache-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadDocumentAsync_CachesHtml_AfterFirstDownload()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><head><title>Cached</title></head><body></body></html>")
        });
        var httpClient = new HttpClient(handler);
        var cache = new HtmlDocumentCache(httpClient, _tempDir);

        var first = await cache.LoadDocumentAsync("https://example.com/page");
        var second = await cache.LoadDocumentAsync("https://example.com/page");

        Assert.Equal("Cached", ExtractTitle(first));
        Assert.Equal("Cached", ExtractTitle(second));
        Assert.Equal(1, handler.RequestCount);
        Assert.Single(Directory.GetFiles(_tempDir, "*.html"));
    }

    [Fact]
    public async Task LoadDocumentAsync_UsesExistingCache_WhenNetworkFails()
    {
        var seedHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><head><title>FromCache</title></head><body></body></html>")
        });

        var seedCache = new HtmlDocumentCache(new HttpClient(seedHandler), _tempDir);
        await seedCache.LoadDocumentAsync("https://example.com/offline");

        var failingHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Network down"));
        var cache = new HtmlDocumentCache(new HttpClient(failingHandler), _tempDir);

        var doc = await cache.LoadDocumentAsync("https://example.com/offline");

        Assert.Equal("FromCache", ExtractTitle(doc));
        Assert.Equal(0, failingHandler.RequestCount);
    }

    [Fact]
    public async Task LoadDocumentAsync_UsesFreshCache_WhenWithinTtl()
    {
        var seedHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><head><title>FreshCache</title></head><body></body></html>")
        });

        var seedCache = new HtmlDocumentCache(new HttpClient(seedHandler), _tempDir);
        await seedCache.LoadDocumentAsync("https://example.com/fresh");

        var cacheFile = Assert.Single(Directory.GetFiles(_tempDir, "*.html"));
        File.SetLastWriteTimeUtc(cacheFile, DateTime.UtcNow.AddMinutes(-5));

        var failingHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Should not be called"));
        var cache = new HtmlDocumentCache(new HttpClient(failingHandler), _tempDir, TimeSpan.FromMinutes(10));

        var doc = await cache.LoadDocumentAsync("https://example.com/fresh");

        Assert.Equal("FreshCache", ExtractTitle(doc));
        Assert.Equal(0, failingHandler.RequestCount);
    }

    [Fact]
    public async Task LoadDocumentAsync_RefreshesExpiredCache_WhenPastTtl()
    {
        var seedHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><head><title>OldValue</title></head><body></body></html>")
        });

        var seedCache = new HtmlDocumentCache(new HttpClient(seedHandler), _tempDir);
        await seedCache.LoadDocumentAsync("https://example.com/stale");

        var cacheFile = Assert.Single(Directory.GetFiles(_tempDir, "*.html"));
        File.SetLastWriteTimeUtc(cacheFile, DateTime.UtcNow.AddMinutes(-30));

        var refreshHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><head><title>NewValue</title></head><body></body></html>")
        });
        var cache = new HtmlDocumentCache(new HttpClient(refreshHandler), _tempDir, TimeSpan.FromMinutes(10));

        var doc = await cache.LoadDocumentAsync("https://example.com/stale");

        Assert.Equal("NewValue", ExtractTitle(doc));
        Assert.Equal(1, refreshHandler.RequestCount);
    }

    private static string ExtractTitle(string html)
    {
        var match = TitleRegex().Match(html);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    [GeneratedRegex(@"<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = responseFactory(request);
            return Task.FromResult(response);
        }
    }
}
