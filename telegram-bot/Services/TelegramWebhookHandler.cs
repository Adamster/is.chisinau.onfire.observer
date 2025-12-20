using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class TelegramWebhookHandler
{
    private const string ManualStreetOption = "Enter manually";
    private readonly IncidentCandidateStore _store;
    private readonly ITelegramNotifier _notifier;
    private readonly IIncidentRepository _repository;
    private readonly IArticleDetailsFetcher _articleDetailsFetcher;
    private readonly IOptionsMonitor<TelegramBotOptions> _options;
    private readonly IOptionsMonitor<RssOptions> _rssOptions;
    private readonly IOptionsMonitor<SupabaseOptions> _supabaseOptions;
    private readonly ILogger<TelegramWebhookHandler> _logger;

    public TelegramWebhookHandler(
        IncidentCandidateStore store,
        ITelegramNotifier notifier,
        IIncidentRepository repository,
        IArticleDetailsFetcher articleDetailsFetcher,
        IOptionsMonitor<TelegramBotOptions> options,
        IOptionsMonitor<RssOptions> rssOptions,
        IOptionsMonitor<SupabaseOptions> supabaseOptions,
        ILogger<TelegramWebhookHandler> logger)
    {
        _store = store;
        _notifier = notifier;
        _repository = repository;
        _articleDetailsFetcher = articleDetailsFetcher;
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

        if (await TryHandleManualStreetAsync(update, cancellationToken))
        {
            return true;
        }

        var callback = update.CallbackQuery;
        if (callback?.Data is null)
        {
            _logger.LogDebug("Ignoring non-callback update {UpdateId}.", update.Id);
            return false;
        }

        if (!IsAuthorized(callback.Message?.Chat?.Id, callback.From?.Id))
        {
            _logger.LogWarning("Ignoring callback from unauthorized chat.");
            await AnswerCallbackAsync(callback, "Not authorized.", showAlert: true, cancellationToken);
            return false;
        }

        if (!CallbackDataParser.TryParse(callback.Data, out var action, out var callbackToken, out var payload))
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

        if (action == ApprovalAction.SelectStreet)
        {
            return await HandleStreetSelectionAsync(callback, candidateId, payload, cancellationToken);
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

        var details = await _articleDetailsFetcher.FetchAsync(pending.Candidate, cancellationToken);
        var streetOptions = BuildStreetOptions(details);

        if (!_store.TrySetStreetOptions(candidateId, streetOptions))
        {
            _logger.LogWarning("Unable to store street options for {CandidateId}.", candidateId);
            await AnswerCallbackAsync(callback, "Unable to prepare street options.", showAlert: true, cancellationToken);
            return false;
        }

        await AnswerCallbackAsync(callback, "Approved. Select a street.", showAlert: true, cancellationToken);
        await _notifier.SendStreetSelectionAsync(
            chatId,
            "Select the street to insert for this incident:",
            streetOptions,
            callbackToken!,
            cancellationToken);

        _logger.LogInformation("Candidate {CandidateId} marked as {Decision}.", candidateId, decision);
        return true;
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

        if (!IsAuthorized(message.Chat?.Id, message.From?.Id))
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

    private static IReadOnlyList<string> BuildStreetOptions(ArticleDetails details)
    {
        var options = details.Streets
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (options.Count == 0)
        {
            options.Add("(unknown)");
        }

        if (!options.Contains(ManualStreetOption, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(ManualStreetOption);
        }

        return options;
    }

    private bool IsAuthorized(long? chatId, long? userId)
    {
        var expectedChatId = _options.CurrentValue.ChatId;
        if (string.IsNullOrWhiteSpace(expectedChatId))
        {
            return true;
        }

        return chatId?.ToString() == expectedChatId || userId?.ToString() == expectedChatId;
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

    private async Task<bool> HandleStreetSelectionAsync(
        CallbackQuery callback,
        string candidateId,
        string? selection,
        CancellationToken cancellationToken)
    {
        if (!_store.TryGetCandidate(candidateId, out var pending) || pending is null)
        {
            _logger.LogWarning("Candidate {CandidateId} could not be loaded for street selection.", candidateId);
            await AnswerCallbackAsync(callback, "This action has expired.", showAlert: true, cancellationToken);
            return false;
        }

        if (!int.TryParse(selection, out var index))
        {
            _logger.LogWarning("Invalid street selection payload: {Payload}", selection);
            await AnswerCallbackAsync(callback, "Invalid street selection.", showAlert: true, cancellationToken);
            return false;
        }

        if (!_store.TryGetStreetOptions(candidateId, out var options) || options is null || index < 0 || index >= options.Count)
        {
            _logger.LogWarning("Street selection out of range for {CandidateId}.", candidateId);
            await AnswerCallbackAsync(callback, "Unknown street selection.", showAlert: true, cancellationToken);
            return false;
        }

        var selectedStreet = options[index];

        var chat = callback.Message?.Chat?.Id ?? callback.From?.Id;
        if (!chat.HasValue)
        {
            _logger.LogWarning("Unable to send response for candidate {CandidateId} due to missing chat id.", candidateId);
            return false;
        }

        await RemoveInlineKeyboardAsync(callback, cancellationToken);

        var chatId = chat.Value.ToString();

        if (string.Equals(selectedStreet, ManualStreetOption, StringComparison.OrdinalIgnoreCase))
        {
            if (!_store.TryBeginManualStreet(candidateId, chatId))
            {
                _logger.LogWarning("Manual street selection could not be started for {CandidateId}.", candidateId);
                await AnswerCallbackAsync(callback, "Already awaiting a manual street.", showAlert: true, cancellationToken);
                return false;
            }

            await AnswerCallbackAsync(callback, "Send the street name in chat.", showAlert: true, cancellationToken);
            await _notifier.SendMessageAsync(chatId, "Please type the street name to use for this incident.", cancellationToken);
            return true;
        }

        if (!_store.TrySelectStreet(candidateId, selectedStreet))
        {
            _logger.LogWarning("Street selection could not be updated for {CandidateId}.", candidateId);
            await AnswerCallbackAsync(callback, "Street already selected.", showAlert: true, cancellationToken);
            return false;
        }

        return await PersistSelectedStreetAsync(candidateId, pending, selectedStreet, chatId, cancellationToken, callback);
    }

    private async Task<bool> TryHandleManualStreetAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message?.Text is null)
        {
            return false;
        }

        if (message.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsAuthorized(message.Chat?.Id, message.From?.Id))
        {
            _logger.LogWarning("Ignoring manual street entry from unauthorized chat.");
            return false;
        }

        var chatId = (message.Chat?.Id ?? message.From?.Id)?.ToString();
        if (string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("Manual street entry missing chat id.");
            return false;
        }

        if (!_store.TryGetManualStreetRequest(chatId, out var candidateId) || string.IsNullOrWhiteSpace(candidateId))
        {
            return false;
        }

        if (!_store.TryGetCandidate(candidateId, out var pending) || pending is null)
        {
            _logger.LogWarning("Manual street entry candidate {CandidateId} could not be loaded.", candidateId);
            _store.ClearManualStreetRequest(chatId, candidateId);
            return false;
        }

        var selectedStreet = message.Text.Trim();
        if (string.IsNullOrWhiteSpace(selectedStreet))
        {
            await _notifier.SendMessageAsync(chatId, "Please send a valid street name.", cancellationToken);
            return true;
        }

        if (!_store.TrySelectManualStreet(candidateId, selectedStreet))
        {
            _logger.LogWarning("Manual street selection could not be updated for {CandidateId}.", candidateId);
            await _notifier.SendMessageAsync(chatId, "Street already selected.", cancellationToken);
            _store.ClearManualStreetRequest(chatId, candidateId);
            return true;
        }

        _store.ClearManualStreetRequest(chatId, candidateId);
        return await PersistSelectedStreetAsync(candidateId, pending, selectedStreet, chatId, cancellationToken);
    }

    private async Task<bool> PersistSelectedStreetAsync(
        string candidateId,
        PendingIncident pending,
        string selectedStreet,
        string chatId,
        CancellationToken cancellationToken,
        CallbackQuery? callback = null)
    {
        if (!_store.TryBeginPersisting(candidateId))
        {
            if (callback is not null)
            {
                await AnswerCallbackAsync(callback, "Already processing.", showAlert: true, cancellationToken);
            }

            await _notifier.SendMessageAsync(
                chatId,
                "This article is already being processed.",
                cancellationToken);
            return true;
        }

        if (callback is not null)
        {
            await AnswerCallbackAsync(callback, $"Selected: {selectedStreet}", showAlert: true, cancellationToken);
        }

        await _notifier.SendMessageAsync(chatId, $"Selected street: {selectedStreet}. Processing.", cancellationToken);

        try
        {
            var inserted = await _repository.AddIncidentAsync(pending.Candidate, selectedStreet, cancellationToken);
            _store.TryMarkPersisted(candidateId);

            var response = inserted is null
                ? "Supabase is not configured, so the approved article was not inserted."
                : BuildApprovalResponse(inserted);

            await _notifier.SendMessageAsync(chatId, response, cancellationToken);
            _logger.LogInformation("Candidate {CandidateId} inserted after street selection.", candidateId);
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
}
