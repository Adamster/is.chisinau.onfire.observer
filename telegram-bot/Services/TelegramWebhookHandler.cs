using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class TelegramWebhookHandler
{
    private readonly IncidentCandidateStore _store;
    private readonly ITelegramNotifier _notifier;
    private readonly IIncidentRepository _repository;
    private readonly IOptionsMonitor<TelegramBotOptions> _options;
    private readonly IOptionsMonitor<RssOptions> _rssOptions;
    private readonly IOptionsMonitor<SupabaseOptions> _supabaseOptions;
    private readonly ILogger<TelegramWebhookHandler> _logger;

    public TelegramWebhookHandler(
        IncidentCandidateStore store,
        ITelegramNotifier notifier,
        IIncidentRepository repository,
        IOptionsMonitor<TelegramBotOptions> options,
        IOptionsMonitor<RssOptions> rssOptions,
        IOptionsMonitor<SupabaseOptions> supabaseOptions,
        ILogger<TelegramWebhookHandler> logger)
    {
        _store = store;
        _notifier = notifier;
        _repository = repository;
        _options = options;
        _rssOptions = rssOptions;
        _supabaseOptions = supabaseOptions;
        _logger = logger;
    }

    public async Task<bool> HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        if (await TryHandleStartAsync(update, cancellationToken))
        {
            return true;
        }

        var callback = update.CallbackQuery;
        if (callback?.Data is null)
        {
            _logger.LogDebug("Ignoring non-callback update {UpdateId}.", update.Id);
            return false;
        }

        if (!IsAuthorized(callback.Message?.Chat?.Id))
        {
            _logger.LogWarning("Ignoring callback from unauthorized chat.");
            await AnswerCallbackAsync(callback, "Not authorized.", showAlert: true, cancellationToken);
            return false;
        }

        if (!CallbackDataParser.TryParse(callback.Data, out var action, out var callbackToken))
        {
            _logger.LogWarning("Unable to parse callback data: {Data}", callback.Data);
            await AnswerCallbackAsync(callback, "Unable to parse action.", showAlert: true, cancellationToken);
            return false;
        }

        if (!_store.TryGetCandidateId(callbackToken!, out var candidateId) || string.IsNullOrWhiteSpace(candidateId))
        {
            _logger.LogWarning("Unable to resolve callback token: {CallbackToken}", callbackToken);
            await AnswerCallbackAsync(callback, "This action has expired.", showAlert: true, cancellationToken);
            return false;
        }

        var decision = action == ApprovalAction.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        var updated = _store.TrySetDecision(candidateId, decision);

        if (!updated)
        {
            _logger.LogWarning("Candidate decision could not be updated for {CandidateId}.", candidateId);
            await AnswerCallbackAsync(callback, "Unable to update this item.", showAlert: true, cancellationToken);
            return false;
        }

        if (!_store.TryGetCandidate(candidateId, out var pending) || pending is null)
        {
            _logger.LogWarning("Candidate {CandidateId} could not be loaded after decision.", candidateId);
            return false;
        }

        var chat = callback.Message?.Chat?.Id ?? callback.From?.Id;
        if (!chat.HasValue)
        {
            _logger.LogWarning("Unable to send response for candidate {CandidateId} due to missing chat id.", candidateId);
            return false;
        }

        await RemoveInlineKeyboardAsync(callback, cancellationToken);
        await UpdateMessageStatusAsync(callback, decision, cancellationToken);

        var chatId = chat.Value.ToString();

        if (decision == ApprovalDecision.Rejected)
        {
            await AnswerCallbackAsync(callback, "Rejected.", showAlert: true, cancellationToken);
            await _notifier.SendMessageAsync(
                chatId,
                "This article will be ignored and will not be considered.",
                cancellationToken);
            _logger.LogInformation("Candidate {CandidateId} marked as {Decision}.", candidateId, decision);
            return true;
        }

        if (!_store.TryBeginPersisting(candidateId))
        {
            await AnswerCallbackAsync(callback, "Already processing.", showAlert: true, cancellationToken);
            await _notifier.SendMessageAsync(
                chatId,
                "This article is already being processed.",
                cancellationToken);
            return true;
        }

        await AnswerCallbackAsync(callback, "Approved. Processing.", showAlert: true, cancellationToken);
        await _notifier.SendMessageAsync(chatId, "Approved. Processing.", cancellationToken);

        try
        {
            var inserted = await _repository.AddIncidentAsync(pending.Candidate, cancellationToken);
            _store.TryMarkPersisted(candidateId);

            var response = inserted is null
                ? "Supabase is not configured, so the approved article was not inserted."
                : BuildApprovalResponse(inserted);

            await _notifier.SendMessageAsync(chatId, response, cancellationToken);
            _logger.LogInformation("Candidate {CandidateId} marked as {Decision}.", candidateId, decision);
            return true;
        }
        catch (Exception ex)
        {
            _store.CancelPersisting(candidateId);
            _logger.LogError(ex, "Failed to insert approved incident for {CandidateId}.", candidateId);
            await _notifier.SendMessageAsync(
                chatId,
                "Failed to insert the approved article into Supabase. Please retry.",
                cancellationToken);
            return false;
        }
    }

    private async Task<bool> TryHandleStartAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message?.Text is null)
        {
            return false;
        }

        if (!message.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsAuthorized(message.Chat?.Id))
        {
            _logger.LogWarning("Ignoring /start from unauthorized chat.");
            return false;
        }

        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            _logger.LogInformation("Ignoring /start because Telegram is disabled.");
            return false;
        }

        var response = BuildConfigurationMessage();
        await _notifier.SendMessageAsync(message.Chat?.Id.ToString() ?? "", response, cancellationToken);
        return true;
    }

    private string BuildConfigurationMessage()
    {
        var rss = _rssOptions.CurrentValue;
        var supabase = _supabaseOptions.CurrentValue;
        var hasSupabase = !string.IsNullOrWhiteSpace(supabase.ConnectionString) ||
                          (!string.IsNullOrWhiteSpace(supabase.Url) && !string.IsNullOrWhiteSpace(supabase.ServiceRoleKey));

        return string.Join(Environment.NewLine, new[]
        {
            "Configuration",
            $"RSS feed: {rss.FeedUrl ?? "(not set)"}",
            $"RSS poll interval: {rss.PollIntervalSeconds}s",
            $"RSS keywords: {rss.Keywords.Count}",
            $"Supabase configured: {(hasSupabase ? "yes" : "no")}"
        });
    }

    private static string BuildApprovalResponse(FireIncident inserted)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "Approved and inserted into Supabase:",
            $"Datetime (UTC): {inserted.Datetime:O}",
            $"Street: {inserted.Street}",
            $"Photo URL: {inserted.PhotoUrl}"
        });
    }

    private bool IsAuthorized(long? chatId)
    {
        var expectedChatId = _options.CurrentValue.ChatId;
        if (string.IsNullOrWhiteSpace(expectedChatId))
        {
            return true;
        }

        return chatId?.ToString() == expectedChatId;
    }

    private Task AnswerCallbackAsync(
        CallbackQuery callback,
        string message,
        bool showAlert,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callback.Id))
        {
            return Task.CompletedTask;
        }

        return _notifier.AnswerCallbackAsync(callback.Id, message, showAlert, cancellationToken);
    }

    private Task RemoveInlineKeyboardAsync(CallbackQuery callback, CancellationToken cancellationToken)
    {
        if (callback.Message?.Chat?.Id is not { } chatId || callback.Message?.MessageId is not { } messageId)
        {
            return Task.CompletedTask;
        }

        return _notifier.RemoveInlineKeyboardAsync(chatId, messageId, cancellationToken);
    }

    private Task UpdateMessageStatusAsync(
        CallbackQuery callback,
        ApprovalDecision decision,
        CancellationToken cancellationToken)
    {
        if (callback.Message?.Chat?.Id is not { } chatId ||
            callback.Message?.MessageId is not { } messageId)
        {
            return Task.CompletedTask;
        }

        var updated = BuildStatusMessage(callback.Message.Text, decision);
        if (string.IsNullOrWhiteSpace(updated))
        {
            return Task.CompletedTask;
        }

        return _notifier.UpdateMessageTextAsync(chatId, messageId, updated, cancellationToken);
    }

    private static string? BuildStatusMessage(string? message, ApprovalDecision decision)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        const string statusMarker = "<b>Status:</b>";
        if (message.Contains(statusMarker, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var decisionText = decision == ApprovalDecision.Approved ? "Approved" : "Rejected";
        return string.Join(Environment.NewLine, new[]
        {
            message,
            string.Empty,
            $"{statusMarker} {decisionText}"
        });
    }
}
