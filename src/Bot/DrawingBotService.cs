using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public sealed class DrawingBotService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DrawingBotService> _logger;
    private readonly ChatStateManager _state;
    private readonly long _allowedUserId;
    private readonly ITelegramBotClient _bot;
    private int _offset;

    public DrawingBotService(
        IServiceProvider services,
        ILogger<DrawingBotService> logger,
        ChatStateManager state,
        IOptions<TelegramOptions> telegram)
    {
        _services = services;
        _logger = logger;
        _state = state;

        var token = telegram.Value.BotToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("TELEGRAM_BOT_TOKEN is required.");
        }

        var allowedUserId = telegram.Value.AllowedUserId;
        if (allowedUserId is null)
        {
            throw new InvalidOperationException("TELEGRAM_ALLOWED_USER_ID is required and must be a number.");
        }

        _allowedUserId = allowedUserId.Value;
        _bot = new TelegramBotClient(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureTelegramReadyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _bot.GetUpdatesAsync(
                    offset: _offset,
                    timeout: 30,
                    cancellationToken: stoppingToken);

                if (updates.Length > 0)
                {
                    _logger.LogInformation("Received {UpdateCount} update(s)", updates.Length);
                }

                foreach (var update in updates)
                {
                    _offset = update.Id + 1;
                    await HandleUpdateAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update polling failed; retrying soon");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task EnsureTelegramReadyAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var me = await _bot.GetMeAsync(stoppingToken);
                _logger.LogInformation("Drawing bot started as @{Username}; allowed user id: {AllowedUserId}", me.Username, _allowedUserId);

                try
                {
                    await _bot.DeleteWebhookAsync(cancellationToken: stoppingToken);
                    _logger.LogInformation("Webhook cleared; using long polling mode");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clear webhook; long polling may not receive updates");
                }

                try
                {
                    await _bot.SetMyCommandsAsync(
                    [
                        new BotCommand { Command = "draw", Description = "Get drawing reference for a subject" },
                        new BotCommand { Command = "random", Description = "Suggest random drawing topic" },
                        new BotCommand { Command = "randomref", Description = "Get a random drawing reference" },
                        new BotCommand { Command = "help", Description = "Show usage help" }
                    ],
                    cancellationToken: stoppingToken);
                    _logger.LogInformation("Telegram command menu updated");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update Telegram command menu");
                }

                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram API is unreachable during startup; retrying in 5 seconds");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        if (update.Message is { } msg)
        {
            await HandleMessageAsync(msg, ct);
            return;
        }

        if (update.CallbackQuery is { } cb)
        {
            await HandleCallbackAsync(cb, ct);
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        if (message.Text is null)
        {
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From?.Id;
        var text = message.Text.Trim();

        _logger.LogInformation("Message received from user {UserId} in chat {ChatId}: {Text}", userId, chatId, text);

        if (userId != _allowedUserId)
        {
            _logger.LogWarning("Unauthorized message from user {UserId}. Allowed user is {AllowedUserId}", userId, _allowedUserId);
            await _bot.SendTextMessageAsync(chatId, "Unauthorized.", cancellationToken: ct);
            return;
        }

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "Use /draw <subject> to get a drawing reference.\nUse /random to get a random topic suggestion.\nUse /randomref to get a random drawing reference.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: ct);
            return;
        }

        if (text.StartsWith("/randomref", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/random_reference", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("random reference", StringComparison.OrdinalIgnoreCase))
        {
            await SendRandomDrawingReferenceAsync(chatId, null, ct);
            return;
        }

        if (text.StartsWith("/random", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("random topic", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            await SuggestRandomTopicAsync(chatId, ct);
            return;
        }

        if (text.StartsWith("/draw", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/drawingref", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/drawing_reference", StringComparison.OrdinalIgnoreCase))
        {
            var subject = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(subject))
            {
                await SuggestRandomTopicAsync(chatId, ct);
                return;
            }

            await SendDrawingReferenceAsync(chatId, subject, null, ct);
            return;
        }

        if (DrawingSubjectParser.TryExtractSubject(text, out var extracted))
        {
            await SendDrawingReferenceAsync(chatId, extracted!, null, ct);
            return;
        }

        await _bot.SendTextMessageAsync(
            chatId,
            "Unknown command. Try /draw hands, /random, or /randomref",
            replyMarkup: BuildMainKeyboard(),
            cancellationToken: ct);
    }

    private async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken ct)
    {
        var chatId = callbackQuery.Message?.Chat.Id;
        var userId = callbackQuery.From.Id;
        var data = callbackQuery.Data;

        _logger.LogInformation("Callback received from user {UserId} in chat {ChatId}: {Data}", userId, chatId, data);

        if (chatId is null || string.IsNullOrWhiteSpace(data))
        {
            await _bot.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: ct);
            return;
        }

        if (userId != _allowedUserId)
        {
            await _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Unauthorized.", cancellationToken: ct);
            return;
        }

        if (!data.StartsWith("draw:", StringComparison.Ordinal))
        {
            await _bot.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: ct);
            return;
        }

        var action = data["draw:".Length..];
        await _bot.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: ct);

        if (action == "confirm")
        {
            var topic = _state.GetAndRemovePendingTopic(chatId.Value);
            if (topic is null)
            {
                await _bot.SendTextMessageAsync(chatId.Value, "Session expired. Use /draw again.", cancellationToken: ct);
                return;
            }

            await SendDrawingReferenceAsync(chatId.Value, topic, null, ct);
            return;
        }

        if (action == "another")
        {
            await SuggestRandomTopicAsync(chatId.Value, ct, editMessage: callbackQuery.Message);
            return;
        }

        if (action == "different_image")
        {
            if (_state.GetLastWasRandomReference(chatId.Value))
            {
                await SendRandomDrawingReferenceAsync(chatId.Value, null, ct);
                return;
            }

            var subject = _state.GetLastSubject(chatId.Value);
            if (string.IsNullOrWhiteSpace(subject))
            {
                await _bot.SendTextMessageAsync(chatId.Value, "No previous subject found. Try /draw <subject>", cancellationToken: ct);
                return;
            }

            await SendDrawingReferenceAsync(chatId.Value, subject, null, ct);
            return;
        }

        if (action == "try_other_source")
        {
            var lastSource = _state.GetLastSource(chatId.Value);
            var forcedSource = lastSource?.Equals("pexels", StringComparison.OrdinalIgnoreCase) == true ? "unsplash" : "pexels";

            if (_state.GetLastWasRandomReference(chatId.Value))
            {
                await SendRandomDrawingReferenceAsync(chatId.Value, forcedSource, ct);
                return;
            }

            var subject = _state.GetLastSubject(chatId.Value);
            if (string.IsNullOrWhiteSpace(subject))
            {
                await _bot.SendTextMessageAsync(chatId.Value, "No previous subject found. Try /draw <subject>", cancellationToken: ct);
                return;
            }

            await SendDrawingReferenceAsync(chatId.Value, subject, forcedSource, ct);
            return;
        }

        if (action == "different_subject")
        {
            await SuggestRandomTopicAsync(chatId.Value, ct);
        }
    }

    private async Task SuggestRandomTopicAsync(long chatId, CancellationToken ct, Message? editMessage = null)
    {
        using var scope = _services.CreateScope();
        var topicService = scope.ServiceProvider.GetRequiredService<IRandomDrawingTopicService>();
        var topic = topicService.GetRandomTopic();
        _state.SetPendingTopic(chatId, topic);

        if (editMessage is not null)
        {
            await _bot.EditMessageTextAsync(
                chatId,
                editMessage.MessageId,
                $"How about drawing: \"{topic}\"?",
                replyMarkup: BuildTopicKeyboard(),
                cancellationToken: ct);
            return;
        }

        await _bot.SendTextMessageAsync(
            chatId,
            $"How about drawing: \"{topic}\"?",
            replyMarkup: BuildTopicKeyboard(),
            cancellationToken: ct);
    }

    private async Task SendDrawingReferenceAsync(long chatId, string subject, string? forcedSource, CancellationToken ct)
    {
        await _bot.SendTextMessageAsync(chatId, "Finding a drawing reference...", cancellationToken: ct);

        try
        {
            using var scope = _services.CreateScope();
            var translator = scope.ServiceProvider.GetRequiredService<ISubjectTranslator>();
            var service = scope.ServiceProvider.GetRequiredService<ICompositeDrawingReferenceService>();

            var original = subject.Trim();
            var translated = await translator.TranslateToEnglishAsync(original, ct);
            if (string.IsNullOrWhiteSpace(translated))
            {
                translated = original;
            }

            DrawingReferenceResult? result;
            if (string.IsNullOrWhiteSpace(forcedSource))
            {
                result = await service.GetReferenceAsync(translated, ct);
            }
            else
            {
                var source = forcedSource.Equals("pexels", StringComparison.OrdinalIgnoreCase)
                    ? ImageSource.Pexels
                    : ImageSource.Unsplash;
                result = await service.GetReferenceFromSourceAsync(translated, source, ct);
            }

            if (result is null)
            {
                await _bot.SendTextMessageAsync(chatId, $"I couldn't find a drawing reference for \"{original}\".", cancellationToken: ct);
                return;
            }

            _state.SetLastSubject(chatId, original);
            _state.SetLastSource(chatId, result.Value.Source.ToString().ToLowerInvariant());
            _state.SetLastWasRandomReference(chatId, false);

            var sourceName = result.Value.Source == ImageSource.Unsplash ? "Unsplash" : "Pexels";
            var header = string.Equals(original, translated, StringComparison.OrdinalIgnoreCase)
                ? $"Drawing reference for \"{original}\":"
                : $"Drawing reference for \"{original}\" (searching: \"{translated}\"):";

            var message = header + "\n" +
                          $"{result.Value.ImageUrl}\n" +
                          $"Photo by {result.Value.PhotographerName} on {sourceName}: {result.Value.PhotoPageUrl}";

            await _bot.SendTextMessageAsync(chatId, message, replyMarkup: BuildResultKeyboard(result.Value.Source), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate drawing reference");
            await _bot.SendTextMessageAsync(chatId, $"Failed to generate drawing reference: {ex.Message}", cancellationToken: ct);
        }
    }

    private async Task SendRandomDrawingReferenceAsync(long chatId, string? forcedSource, CancellationToken ct)
    {
        await _bot.SendTextMessageAsync(chatId, "Finding a random drawing reference...", cancellationToken: ct);

        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICompositeDrawingReferenceService>();

            DrawingReferenceResult? result;
            if (string.IsNullOrWhiteSpace(forcedSource))
            {
                result = await service.GetRandomReferenceAsync(ct);
            }
            else
            {
                var source = forcedSource.Equals("pexels", StringComparison.OrdinalIgnoreCase)
                    ? ImageSource.Pexels
                    : ImageSource.Unsplash;
                result = await service.GetRandomReferenceFromSourceAsync(source, ct);
            }

            if (result is null)
            {
                await _bot.SendTextMessageAsync(chatId, "I couldn't find a random drawing reference.", cancellationToken: ct);
                return;
            }

            _state.SetLastSource(chatId, result.Value.Source.ToString().ToLowerInvariant());
            _state.SetLastWasRandomReference(chatId, true);

            var sourceName = result.Value.Source == ImageSource.Unsplash ? "Unsplash" : "Pexels";
            var message = "Random drawing reference:\n" +
                          $"{result.Value.ImageUrl}\n" +
                          $"Photo by {result.Value.PhotographerName} on {sourceName}: {result.Value.PhotoPageUrl}";

            await _bot.SendTextMessageAsync(chatId, message, replyMarkup: BuildResultKeyboard(result.Value.Source), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate random drawing reference");
            await _bot.SendTextMessageAsync(chatId, $"Failed to generate random drawing reference: {ex.Message}", cancellationToken: ct);
        }
    }

    private static InlineKeyboardMarkup BuildTopicKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Yes, let's go!", "draw:confirm"),
                InlineKeyboardButton.WithCallbackData("Suggest another", "draw:another")
            }
        });
    }

    private static InlineKeyboardMarkup BuildResultKeyboard(ImageSource currentSource)
    {
        var tryOtherLabel = currentSource == ImageSource.Unsplash ? "Try Pexels" : "Try Unsplash";

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Different Image", "draw:different_image"),
                InlineKeyboardButton.WithCallbackData(tryOtherLabel, "draw:try_other_source")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Different Subject", "draw:different_subject")
            }
        });
    }

    private static ReplyKeyboardMarkup BuildMainKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("random topic"),
                new KeyboardButton("random reference")
            }
        })
        {
            ResizeKeyboard = true,
            IsPersistent = true,
            InputFieldPlaceholder = "Type /draw <subject>, /random, or /randomref"
        };
    }
}
