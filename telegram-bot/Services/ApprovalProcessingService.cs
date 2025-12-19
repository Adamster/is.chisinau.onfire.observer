using Microsoft.Extensions.Options;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class ApprovalProcessingService : BackgroundService
{
    private readonly IncidentCandidateStore _store;
    private readonly IIncidentRepository _repository;
    private readonly IOptionsMonitor<SupabaseOptions> _options;
    private readonly ILogger<ApprovalProcessingService> _logger;

    public ApprovalProcessingService(
        IncidentCandidateStore store,
        IIncidentRepository repository,
        IOptionsMonitor<SupabaseOptions> options,
        ILogger<ApprovalProcessingService> logger)
    {
        _store = store;
        _repository = repository;
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
                var approved = _store.GetAll()
                    .Where(item => item.Decision == ApprovalDecision.Approved &&
                                   !item.IsPersisted &&
                                   !string.IsNullOrWhiteSpace(item.SelectedStreet))
                    .ToList();

                foreach (var incident in approved)
                {
                    if (!_store.TryBeginPersisting(incident.Candidate.Id))
                    {
                        continue;
                    }

                    try
                    {
                        await _repository.AddIncidentAsync(incident.Candidate, incident.SelectedStreet, stoppingToken);
                        _store.TryMarkPersisted(incident.Candidate.Id);
                    }
                    catch
                    {
                        _store.CancelPersisting(incident.Candidate.Id);
                        throw;
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to persist approved incidents.");
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
