using Microsoft.Extensions.Options;

public static class Bootstrap
{
    public static void AddEnvironmentOverrides(this HostApplicationBuilder builder)
    {
        var envOverrides = new Dictionary<string, string?>();

        MapEnv("TELEGRAM_BOT_TOKEN", "Telegram:BotToken");
        MapEnv("TELEGRAM_ALLOWED_USER_ID", "Telegram:AllowedUserId");
        MapEnv("AI_BASE_URL", "AiDefaults:BaseUrl");
        MapEnv("AI_MODEL", "AiDefaults:Model");
        MapEnv("AI_API_KEY", "AiDefaults:ApiKey");
        MapEnv("UNSPLASH_ACCESS_KEY", "Unsplash:AccessKey");
        MapEnv("PEXELS_API_KEY", "Pexels:ApiKey");

        if (envOverrides.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(envOverrides);
        }

        return;

        void MapEnv(string envVar, string configKey)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(value))
            {
                envOverrides[configKey] = value;
            }
        }
    }

    public static IServiceCollection AddDrawingBotServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
        services.Configure<AiDefaults>(configuration.GetSection("AiDefaults"));
        services.Configure<UnsplashOptions>(configuration.GetSection("Unsplash"));
        services.Configure<PexelsOptions>(configuration.GetSection("Pexels"));

        services.AddSingleton<ChatStateManager>();
        services.AddSingleton<IRandomDrawingTopicService, RandomDrawingTopicService>();
        services.AddHttpClient<UnsplashDrawingReferenceService>();
        services.AddHttpClient<PexelsDrawingReferenceService>();
        services.AddScoped<ICompositeDrawingReferenceService, CompositeDrawingReferenceService>();
        services.AddHttpClient<ISubjectTranslator, OpenAiSubjectTranslator>();

        services.AddHostedService<DrawingBotService>();
        return services;
    }
}
