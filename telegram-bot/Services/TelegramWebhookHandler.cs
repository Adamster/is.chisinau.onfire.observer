using Microsoft.Extensions.Options;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class TelegramWebhookHandler
{
    private readonly IncidentCandidateStore _store;
    private readonly IOptionsMonitor<TelegramBotOptions> _options;
    private readonly ILogger<TelegramWebhookHandler> _logger;

    public TelegramWebhookHandler(
        IncidentCandidateStore store,
        IOptionsMonitor<TelegramBotOptions> options,
        ILogger<TelegramWebhookHandler> logger)
    {
        _store = store;
        _options = options;
        _logger = logger;
    }

    public bool HandleUpdate(TelegramUpdate update)
    {
        var callback = update.CallbackQuery;
        if (callback?.Data is null)
        {
            _logger.LogDebug("Ignoring non-callback update {UpdateId}.", update.UpdateId);
            return false;
        }

        if (!IsAuthorized(callback.Message?.Chat?.Id))
        {
            _logger.LogWarning("Ignoring callback from unauthorized chat.");
            return false;
        }

        if (!CallbackDataParser.TryParse(callback.Data, out var action, out var candidateId))
        {
            _logger.LogWarning("Unable to parse callback data: {Data}", callback.Data);
            return false;
        }

        var decision = action == ApprovalAction.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        var updated = _store.TrySetDecision(candidateId!, decision);

        if (!updated)
        {
            _logger.LogWarning("Candidate decision could not be updated for {CandidateId}.", candidateId);
            return false;
        }

        _logger.LogInformation("Candidate {CandidateId} marked as {Decision}.", candidateId, decision);
        return true;
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
}
