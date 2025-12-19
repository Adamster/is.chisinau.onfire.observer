using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;

namespace TelegramBot.Services;

public sealed class TelegramWebhookSetupService : IHostedService
{
    private readonly IOptionsMonitor<TelegramBotOptions> _options;
    private readonly ILogger<TelegramWebhookSetupService> _logger;

    public TelegramWebhookSetupService(
        IOptionsMonitor<TelegramBotOptions> options,
        ILogger<TelegramWebhookSetupService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        if (!config.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(config.WebhookUrl))
        {
            _logger.LogInformation("Telegram webhook setup skipped because BotToken or WebhookUrl is missing.");
            return;
        }

        try
        {
            var client = new TelegramBotClient(config.BotToken);
            var request = new SetWebhookRequest
            {
                Url = config.WebhookUrl,
                DropPendingUpdates = false
            };
            await client.SendRequest(request, cancellationToken);
            _logger.LogInformation("Telegram webhook configured for {WebhookUrl}.", config.WebhookUrl);
        }
        catch (Exception ex) when (ex is Telegram.Bot.Exceptions.ApiRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Telegram setWebhook failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
