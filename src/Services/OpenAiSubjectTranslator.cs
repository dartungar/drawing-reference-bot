using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

public interface ISubjectTranslator
{
    Task<string> TranslateToEnglishAsync(string subject, CancellationToken ct = default);
}

public sealed class OpenAiSubjectTranslator : ISubjectTranslator
{
    private readonly HttpClient _httpClient;
    private readonly AiDefaults _settings;
    private readonly ILogger<OpenAiSubjectTranslator> _logger;

    public OpenAiSubjectTranslator(HttpClient httpClient, IOptions<AiDefaults> settings, ILogger<OpenAiSubjectTranslator> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> TranslateToEnglishAsync(string subject, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.Model))
        {
            return subject.Trim();
        }

        var endpoint = ResolveResponsesEndpoint(_settings.BaseUrl);
        var instructions = "Translate drawing subject to short English phrase. Output only translated text.";

        var body = new
        {
            model = _settings.Model,
            instructions,
            input = subject.Trim(),
            reasoning = new { effort = "low" },
            text = new { verbosity = "low" },
            max_output_tokens = 32
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var rawBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Subject translation failed with status {Status}", (int)response.StatusCode);
            return subject.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var translated = ExtractResponsesText(doc.RootElement);
            if (string.IsNullOrWhiteSpace(translated))
            {
                return subject.Trim();
            }

            var firstLine = translated
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? translated;

            return firstLine.Trim().Trim('"', '\'', '.', ':', ';');
        }
        catch
        {
            return subject.Trim();
        }
    }

    private static string ResolveResponsesEndpoint(string? baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        if (!normalized.EndsWith("v1/", StringComparison.OrdinalIgnoreCase))
        {
            normalized += "v1/";
        }

        return normalized + "responses";
    }

    private static string ExtractResponsesText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var c in content.EnumerateArray())
            {
                if (c.TryGetProperty("type", out var type) &&
                    type.ValueKind == JsonValueKind.String &&
                    type.GetString() == "output_text" &&
                    c.TryGetProperty("text", out var textEl) &&
                    textEl.ValueKind == JsonValueKind.String)
                {
                    sb.AppendLine(textEl.GetString());
                }
            }
        }

        return sb.ToString().Trim();
    }
}
