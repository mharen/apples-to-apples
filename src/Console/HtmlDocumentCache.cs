using System.Security.Cryptography;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;

namespace ApplesToApples.ConsoleApp;

public sealed class HtmlDocumentCache
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly IBrowsingContext _browsingContext;
    private readonly TimeSpan? _cacheTtl;

    public HtmlDocumentCache(HttpClient? httpClient = null, string? cacheDirectory = null, TimeSpan? cacheTtl = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _cacheDirectory = cacheDirectory ?? Path.Combine(AppContext.BaseDirectory, "html-cache");
        _browsingContext = BrowsingContext.New(Configuration.Default);
        _cacheTtl = cacheTtl;
    }

    public async Task<IDocument> LoadDocumentAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("URL must be an absolute URI.", nameof(url));

        var cacheFilePath = GetCacheFilePath(uri);
        string html;

        if (IsCacheValid(cacheFilePath))
        {
            html = await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
        }
        else
        {
            Console.Error.WriteLine($"Fetching: {url}");
            html = await _httpClient.GetStringAsync(uri, cancellationToken);
            Directory.CreateDirectory(_cacheDirectory);
            await File.WriteAllTextAsync(cacheFilePath, html, cancellationToken);
        }

        return await _browsingContext.OpenAsync(request =>
        {
            request.Address(uri);
            request.Content(html);
        }, cancellationToken);
    }

    private string GetCacheFilePath(Uri uri)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri));
        var hash = Convert.ToHexString(bytes).ToLowerInvariant();
        return Path.Combine(_cacheDirectory, $"{hash}.html");
    }

    private bool IsCacheValid(string cacheFilePath)
    {
        if (!File.Exists(cacheFilePath))
        {
            return false;
        }

        if (_cacheTtl is null)
        {
            return true;
        }

        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFilePath);
        return age <= _cacheTtl.Value;
    }
}
