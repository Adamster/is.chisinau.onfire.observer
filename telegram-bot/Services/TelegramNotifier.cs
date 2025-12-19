using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly IOptionsMonitor<TelegramBotOptions> _options;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(IOptionsMonitor<TelegramBotOptions> options, ILogger<TelegramNotifier> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<int?> SendCandidateAsync(RssItemCandidate candidate, string callbackToken, CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken) ||
            string.IsNullOrWhiteSpace(config.ChatId) ||
            string.IsNullOrWhiteSpace(callbackToken))
        {
            _logger.LogWarning("Telegram bot token, chat id, or callback token is missing.");
            return null;
        }

        var replyMarkup = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Approve", $"approve:{callbackToken}"),
                InlineKeyboardButton.WithCallbackData("Reject", $"reject:{callbackToken}")
            }
        });

        return await SendMessageAsync(
            new ChatId(config.ChatId),
            BuildMessage(candidate),
            replyMarkup,
            cancellationToken);
    }

    public async Task<int?> SendMessageAsync(string chatId, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Telegram chat id or message is missing.");
            return null;
        }

        return await SendMessageAsync(
            new ChatId(chatId),
            Escape(message),
            replyMarkup: null,
            cancellationToken);
    }

    public async Task AnswerCallbackAsync(
        string callbackQueryId,
        string message,
        bool showAlert,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callbackQueryId))
        {
            _logger.LogWarning("Telegram callback query id is missing.");
            return;
        }

        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            _logger.LogWarning("Telegram bot token is missing.");
            return;
        }

        try
        {
            var client = new TelegramBotClient(config.BotToken);
            var request = new AnswerCallbackQueryRequest
            {
                CallbackQueryId = callbackQueryId,
                Text = string.IsNullOrWhiteSpace(message) ? null : message,
                ShowAlert = showAlert
            };
            await client.SendRequest(request, cancellationToken);
        }
        catch (Exception ex) when (ex is Telegram.Bot.Exceptions.ApiRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Telegram answerCallbackQuery failed.");
        }
    }

    public async Task RemoveInlineKeyboardAsync(long chatId, int messageId, CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            _logger.LogWarning("Telegram bot token is missing.");
            return;
        }

        try
        {
            var client = new TelegramBotClient(config.BotToken);
            var request = new EditMessageReplyMarkupRequest
            {
                ChatId = new ChatId(chatId),
                MessageId = messageId,
                ReplyMarkup = null
            };
            await client.SendRequest(request, cancellationToken);
        }
        catch (Exception ex) when (ex is Telegram.Bot.Exceptions.ApiRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Telegram editMessageReplyMarkup failed.");
        }
    }

    public async Task UpdateMessageTextAsync(long chatId, int messageId, string message, CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            _logger.LogWarning("Telegram bot token is missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Telegram message text is missing.");
            return;
        }

        try
        {
            var client = new TelegramBotClient(config.BotToken);
            var request = new EditMessageTextRequest
            {
                ChatId = new ChatId(chatId),
                MessageId = messageId,
                Text = message,
                ParseMode = ParseMode.Html
            };
            await client.SendRequest(request, cancellationToken);
        }
        catch (Exception ex) when (ex is Telegram.Bot.Exceptions.ApiRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Telegram editMessageText failed.");
        }
    }

    private async Task<int?> SendMessageAsync(
        ChatId chatId,
        string message,
        ReplyMarkup? replyMarkup,
        CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            _logger.LogWarning("Telegram bot token or chat id is missing.");
            return null;
        }

        try
        {
            var client = new TelegramBotClient(config.BotToken);
            var request = new SendMessageRequest
            {
                ChatId = chatId,
                Text = message,
                ParseMode = ParseMode.Html,
                ReplyMarkup = replyMarkup
            };
            var sent = await client.SendRequest(request, cancellationToken);
            return sent?.MessageId;
        }
        catch (Exception ex) when (ex is Telegram.Bot.Exceptions.ApiRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Telegram sendMessage failed.");
            return null;
        }
    }

    private static string BuildMessage(RssItemCandidate candidate)
    {
        var builder = new StringBuilder();
        var title = StripHtml(candidate.Title);
        builder.AppendLine($"<b>{Escape(title)}</b>");

        if (!string.IsNullOrWhiteSpace(candidate.Link))
        {
            builder.AppendLine($"<a href=\"{EscapeAttribute(candidate.Link)}\">Open article</a>");
        }

        if (candidate.PublishedAt.HasValue)
        {
            builder.AppendLine($"Published: {candidate.PublishedAt:yyyy-MM-dd HH:mm}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Summary))
        {
            builder.AppendLine();
            var summary = StripHtml(candidate.Summary);
            builder.AppendLine(Escape(summary));
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EscapeAttribute(string value) =>
        Escape(value).Replace("\"", "&quot;").Replace("'", "&#39;");

    private static string StripHtml(string value) => Regex.Replace(value, "<.*?>", string.Empty);
}
