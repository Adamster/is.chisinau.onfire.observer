using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class TelegramWebhookHandler
{
    private readonly IncidentCandidateStore _store;
    private readonly ITelegramNotifier _notifier;
    private readonly IOptionsMonitor<TelegramBotOptions> _options;
    private readonly IOptionsMonitor<RssOptions> _rssOptions;
    private readonly IOptionsMonitor<SupabaseOptions> _supabaseOptions;
    private readonly ILogger<TelegramWebhookHandler> _logger;

    public TelegramWebhookHandler(
        IncidentCandidateStore store,
        ITelegramNotifier notifier,
        IOptionsMonitor<TelegramBotOptions> options,
        IOptionsMonitor<RssOptions> rssOptions,
        IOptionsMonitor<SupabaseOptions> supabaseOptions,
        ILogger<TelegramWebhookHandler> logger)
    {
        _store = store;
        _notifier = notifier;
        _options = options;
        _rssOptions = rssOptions;
        _supabaseOptions = supabaseOptions;
        _logger = logger;
    }

    public bool HandleUpdate(Update update)
    {
        if (TryHandleStart(update))
        {
            return true;
        }

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

    private bool TryHandleStart(Update update)
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
        _ = _notifier.SendMessageAsync(message.Chat?.Id.ToString() ?? "", response, CancellationToken.None);
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
