public sealed class TelegramOptions
{
    public string? BotToken { get; set; }
    public long? AllowedUserId { get; set; }
}

public sealed class AiDefaults
{
    public string? BaseUrl { get; set; }
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class UnsplashOptions
{
    public string? AccessKey { get; set; }
}

public sealed class PexelsOptions
{
    public string? ApiKey { get; set; }
}
