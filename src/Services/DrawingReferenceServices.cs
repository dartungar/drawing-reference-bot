using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

public interface IDrawingReferenceService
{
    Task<DrawingReferenceResult?> GetReferenceAsync(string subject, CancellationToken ct = default);
    Task<DrawingReferenceResult?> GetRandomReferenceAsync(CancellationToken ct = default);
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

        using var req = CreateRequest(url, accessKey);

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

        return TryParsePhoto(items[Random.Shared.Next(items.Length)]);
    }

    public async Task<DrawingReferenceResult?> GetRandomReferenceAsync(CancellationToken ct = default)
    {
        var accessKey = _options.AccessKey;
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            return null;
        }

        const string url = "https://api.unsplash.com/photos/random?content_filter=high&orientation=portrait";

        using var req = CreateRequest(url, accessKey);

        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Unsplash request failed: {Status} {Reason}", (int)resp.StatusCode, resp.ReasonPhrase);
            return null;
        }

        using var doc = JsonDocument.Parse(raw);
        return TryParsePhoto(doc.RootElement);
    }

    private static HttpRequestMessage CreateRequest(string url, string accessKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", $"Client-ID {accessKey}");
        req.Headers.TryAddWithoutValidation("Accept-Version", "v1");
        return req;
    }

    private static DrawingReferenceResult? TryParsePhoto(JsonElement photo)
    {
        var imageUrl = photo.GetProperty("urls").GetProperty("regular").GetString();
        var photoPageUrl = photo.GetProperty("links").GetProperty("html").GetString();
        var photographerName = photo.GetProperty("user").GetProperty("name").GetString();
        var photographerProfileUrl = photo.GetProperty("user").GetProperty("links").GetProperty("html").GetString();

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

        return await SendPhotoRequestAsync(url, apiKey, ct);
    }

    public async Task<DrawingReferenceResult?> GetRandomReferenceAsync(CancellationToken ct = default)
    {
        var apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var randomPage = Random.Shared.Next(1, 11);
        var randomUrl = $"https://api.pexels.com/v1/curated?page={randomPage}&per_page=80";
        var result = await SendPhotoRequestAsync(randomUrl, apiKey, ct);
        if (result is not null || randomPage == 1)
        {
            return result;
        }

        return await SendPhotoRequestAsync("https://api.pexels.com/v1/curated?page=1&per_page=80", apiKey, ct);
    }

    private async Task<DrawingReferenceResult?> SendPhotoRequestAsync(string url, string apiKey, CancellationToken ct)
    {
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

        return TryParsePhoto(items[Random.Shared.Next(items.Length)]);
    }

    private static DrawingReferenceResult? TryParsePhoto(JsonElement photo)
    {
        var imageUrl = photo.GetProperty("src").GetProperty("large").GetString();
        var photoPageUrl = photo.GetProperty("url").GetString();
        var photographerName = photo.GetProperty("photographer").GetString();
        var photographerProfileUrl = photo.GetProperty("photographer_url").GetString();

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
    Task<DrawingReferenceResult?> GetRandomReferenceAsync(CancellationToken ct = default);
    Task<DrawingReferenceResult?> GetRandomReferenceFromSourceAsync(ImageSource source, CancellationToken ct = default);
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

    public Task<DrawingReferenceResult?> GetRandomReferenceAsync(CancellationToken ct = default)
    {
        var source = Random.Shared.Next(2) == 0 ? ImageSource.Unsplash : ImageSource.Pexels;
        return GetRandomReferenceFromSourceAsync(source, ct);
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

    public async Task<DrawingReferenceResult?> GetRandomReferenceFromSourceAsync(ImageSource source, CancellationToken ct = default)
    {
        var first = source == ImageSource.Unsplash ? await _unsplash.GetRandomReferenceAsync(ct) : await _pexels.GetRandomReferenceAsync(ct);
        if (first is not null)
        {
            return first;
        }

        return source == ImageSource.Unsplash
            ? await _pexels.GetRandomReferenceAsync(ct)
            : await _unsplash.GetRandomReferenceAsync(ct);
    }
}
