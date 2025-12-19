using Microsoft.Extensions.Options;

namespace TelegramBot.Services;

public sealed class RssPollingService : BackgroundService
{
    private readonly RssFetcher _fetcher;
    private readonly IncidentCandidateStore _store;
    private readonly IOptionsMonitor<RssOptions> _options;
    private readonly ILogger<RssPollingService> _logger;

    public RssPollingService(
        RssFetcher fetcher,
        IncidentCandidateStore store,
        IOptionsMonitor<RssOptions> options,
        ILogger<RssPollingService> logger)
    {
        _fetcher = fetcher;
        _store = store;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(_options.CurrentValue.PollIntervalSeconds);

            try
            {
                var candidates = await _fetcher.FetchCandidatesAsync(stoppingToken);
                var added = 0;

                foreach (var candidate in candidates)
                {
                    if (_store.TryAdd(candidate))
                    {
                        added++;
                    }
                }

                if (added > 0)
                {
                    _logger.LogInformation("Added {Count} RSS candidates.", added);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to poll RSS feed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
