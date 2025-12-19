using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<TelegramBotOptions> _options;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(HttpClient httpClient, IOptionsMonitor<TelegramBotOptions> options, ILogger<TelegramNotifier> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<int?> SendCandidateAsync(RssItemCandidate candidate, CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(config.ChatId))
        {
            _logger.LogWarning("Telegram bot token or chat id is missing.");
            return null;
        }

        var payload = new
        {
            text = BuildMessage(candidate),
            parse_mode = "HTML",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "Approve", callback_data = $"approve:{candidate.Id}" },
                        new { text = "Reject", callback_data = $"reject:{candidate.Id}" }
                    }
                }
            }
        };

        return await SendMessageAsync(config.ChatId, payload, cancellationToken);
    }

    public async Task<int?> SendMessageAsync(string chatId, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var payload = new
        {
            text = Escape(message),
            parse_mode = "HTML"
        };

        return await SendMessageAsync(chatId, payload, cancellationToken);
    }

    private async Task<int?> SendMessageAsync(string? chatId, object payload, CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("Telegram bot token or chat id is missing.");
            return null;
        }

        var wrappedPayload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId
        };

        foreach (var property in payload.GetType().GetProperties())
        {
            wrappedPayload[property.Name] = property.GetValue(payload);
        }

        using var content = new StringContent(JsonSerializer.Serialize(wrappedPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{config.BotToken}/sendMessage", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Telegram sendMessage failed with status {Status}.", response.StatusCode);
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<TelegramSendMessageResponse>(responseBody);
        return parsed?.Result?.MessageId;
    }

    private static string BuildMessage(RssItemCandidate candidate)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<b>{Escape(candidate.Title)}</b>");

        if (!string.IsNullOrWhiteSpace(candidate.Link))
        {
            builder.AppendLine($"<a href=\"{Escape(candidate.Link)}\">Open article</a>");
        }

        if (candidate.PublishedAt.HasValue)
        {
            builder.AppendLine($"Published: {candidate.PublishedAt:yyyy-MM-dd HH:mm}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Summary))
        {
            builder.AppendLine();
            builder.AppendLine(Escape(candidate.Summary));
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private sealed class TelegramSendMessageResponse
    {
        public TelegramMessageResult? Result { get; init; }
    }

    private sealed class TelegramMessageResult
    {
        public int MessageId { get; init; }
    }
}
