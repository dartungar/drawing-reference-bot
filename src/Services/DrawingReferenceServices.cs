using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

public interface IDrawingReferenceService
{
    Task<DrawingReferenceResult?> GetReferenceAsync(string subject, CancellationToken ct = default);
}

public sealed class UnsplashDrawingReferenceService : IDrawingReferenceService
{
    private readonly HttpClient _http;
    private readonly UnsplashOptions _options;
    private readonly ILogger<UnsplashDrawingReferenceService> _logger;

    public UnsplashDrawingReferenceService(HttpClient http, IOptions<UnsplashOptions> options, ILogger<UnsplashDrawingReferenceService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DrawingReferenceResult?> GetReferenceAsync(string subject, CancellationToken ct = default)
    {
        var accessKey = _options.AccessKey;
        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var url = "https://api.unsplash.com/search/photos" +
                  "?query=" + WebUtility.UrlEncode(subject.Trim()) +
                  "&per_page=30&content_filter=high&orientation=portrait";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", $"Client-ID {accessKey}");
        req.Headers.TryAddWithoutValidation("Accept-Version", "v1");

        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Unsplash request failed: {Status} {Reason}", (int)resp.StatusCode, resp.ReasonPhrase);
            return null;
        }

        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = results.EnumerateArray().ToArray();
        if (items.Length == 0)
        {
            return null;
        }

        var selected = items[Random.Shared.Next(items.Length)];
        var imageUrl = selected.GetProperty("urls").GetProperty("regular").GetString();
        var photoPageUrl = selected.GetProperty("links").GetProperty("html").GetString();
        var photographerName = selected.GetProperty("user").GetProperty("name").GetString();
        var photographerProfileUrl = selected.GetProperty("user").GetProperty("links").GetProperty("html").GetString();

        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(photoPageUrl) ||
            string.IsNullOrWhiteSpace(photographerName) || string.IsNullOrWhiteSpace(photographerProfileUrl))
        {
            return null;
        }

        return new DrawingReferenceResult(imageUrl, photoPageUrl, photographerName, photographerProfileUrl, ImageSource.Unsplash);
    }
}

public sealed class PexelsDrawingReferenceService : IDrawingReferenceService
{
    private readonly HttpClient _http;
    private readonly PexelsOptions _options;
    private readonly ILogger<PexelsDrawingReferenceService> _logger;

    public PexelsDrawingReferenceService(HttpClient http, IOptions<PexelsOptions> options, ILogger<PexelsDrawingReferenceService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DrawingReferenceResult?> GetReferenceAsync(string subject, CancellationToken ct = default)
    {
        var apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var url = "https://api.pexels.com/v1/search" +
                  "?query=" + WebUtility.UrlEncode(subject.Trim()) +
                  "&per_page=30&orientation=portrait";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", apiKey);

        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Pexels request failed: {Status} {Reason}", (int)resp.StatusCode, resp.ReasonPhrase);
            return null;
        }

        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("photos", out var photos) || photos.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = photos.EnumerateArray().ToArray();
        if (items.Length == 0)
        {
            return null;
        }

        var selected = items[Random.Shared.Next(items.Length)];
        var imageUrl = selected.GetProperty("src").GetProperty("large").GetString();
        var photoPageUrl = selected.GetProperty("url").GetString();
        var photographerName = selected.GetProperty("photographer").GetString();
        var photographerProfileUrl = selected.GetProperty("photographer_url").GetString();

        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(photoPageUrl) ||
            string.IsNullOrWhiteSpace(photographerName) || string.IsNullOrWhiteSpace(photographerProfileUrl))
        {
            return null;
        }

        return new DrawingReferenceResult(imageUrl, photoPageUrl, photographerName, photographerProfileUrl, ImageSource.Pexels);
    }
}

public interface ICompositeDrawingReferenceService
{
    Task<DrawingReferenceResult?> GetReferenceAsync(string subject, CancellationToken ct = default);
    Task<DrawingReferenceResult?> GetReferenceFromSourceAsync(string subject, ImageSource source, CancellationToken ct = default);
}

public sealed class CompositeDrawingReferenceService : ICompositeDrawingReferenceService
{
    private readonly UnsplashDrawingReferenceService _unsplash;
    private readonly PexelsDrawingReferenceService _pexels;

    public CompositeDrawingReferenceService(UnsplashDrawingReferenceService unsplash, PexelsDrawingReferenceService pexels)
    {
        _unsplash = unsplash;
        _pexels = pexels;
    }

    public Task<DrawingReferenceResult?> GetReferenceAsync(string subject, CancellationToken ct = default)
    {
        var source = Random.Shared.Next(2) == 0 ? ImageSource.Unsplash : ImageSource.Pexels;
        return GetReferenceFromSourceAsync(subject, source, ct);
    }

    public async Task<DrawingReferenceResult?> GetReferenceFromSourceAsync(string subject, ImageSource source, CancellationToken ct = default)
    {
        var first = source == ImageSource.Unsplash ? await _unsplash.GetReferenceAsync(subject, ct) : await _pexels.GetReferenceAsync(subject, ct);
        if (first is not null)
        {
            return first;
        }

        return source == ImageSource.Unsplash
            ? await _pexels.GetReferenceAsync(subject, ct)
            : await _unsplash.GetReferenceAsync(subject, ct);
    }
}
