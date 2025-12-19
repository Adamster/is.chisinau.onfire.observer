using Microsoft.Extensions.Options;
using Supabase;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class SupabaseIncidentRepository : IIncidentRepository
{
    private readonly IOptionsMonitor<SupabaseOptions> _options;
    private readonly ILogger<SupabaseIncidentRepository> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Client? _client;

    public SupabaseIncidentRepository(IOptionsMonitor<SupabaseOptions> options, ILogger<SupabaseIncidentRepository> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task AddIncidentAsync(RssItemCandidate candidate, CancellationToken cancellationToken)
    {
        var url = _options.CurrentValue.Url;
        var key = _options.CurrentValue.ServiceRoleKey;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Supabase URL or service role key is not configured.");
            return;
        }

        var when = candidate.PublishedAt ?? DateTimeOffset.UtcNow;
        var photoUrl = ResolvePhotoUrl(candidate);
        var street = ResolveStreet(candidate);

        var client = await GetClientAsync(url, key, cancellationToken);
        var payload = new FireIncident
        {
            Datetime = when.UtcDateTime,
            PhotoUrl = photoUrl,
            Street = street
        };

        await client.From<FireIncident>().Insert(payload, cancellationToken: cancellationToken);
        _logger.LogInformation("Inserted incident row for {CandidateId}.", candidate.Id);
    }

    private string ResolvePhotoUrl(RssItemCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(_options.CurrentValue.DefaultPhotoUrl))
        {
            return _options.CurrentValue.DefaultPhotoUrl!;
        }

        return string.IsNullOrWhiteSpace(candidate.Link) ? "" : candidate.Link;
    }

    private string ResolveStreet(RssItemCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(_options.CurrentValue.DefaultStreet))
        {
            return _options.CurrentValue.DefaultStreet!;
        }

        return string.IsNullOrWhiteSpace(candidate.Title) ? "(unknown)" : candidate.Title;
    }

    private async Task<Client> GetClientAsync(string url, string key, CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is null)
            {
                var options = new SupabaseOptions
                {
                    AutoConnectRealtime = false,
                    AutoRefreshToken = false
                };

                var client = new Client(url, key, options);
                await client.InitializeAsync();
                _client = client;
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _client!;
    }
}
