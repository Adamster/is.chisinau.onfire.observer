using System.Data.Common;
using Microsoft.Extensions.Options;
using Supabase;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class SupabaseIncidentRepository : IIncidentRepository
{
    private readonly IOptionsMonitor<SupabaseOptions> _options;
    private readonly IArticleDetailsFetcher _articleDetailsFetcher;
    private readonly ILogger<SupabaseIncidentRepository> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Client? _client;

    public SupabaseIncidentRepository(
        IOptionsMonitor<SupabaseOptions> options,
        IArticleDetailsFetcher articleDetailsFetcher,
        ILogger<SupabaseIncidentRepository> logger)
    {
        _options = options;
        _articleDetailsFetcher = articleDetailsFetcher;
        _logger = logger;
    }

    public async Task<FireIncident?> AddIncidentAsync(RssItemCandidate candidate, CancellationToken cancellationToken)
    {
        if (!TryGetConnectionInfo(_options.CurrentValue, out var url, out var key))
        {
            _logger.LogWarning("Supabase connection string or service role key is not configured.");
            return null;
        }

        var payload = await BuildPayloadAsync(candidate, cancellationToken);
        var client = await GetClientAsync(url, key, cancellationToken);

        _logger.LogInformation("Attempting Supabase insert for {CandidateId}.", candidate.Id);
        var response = await client.From<FireIncident>().Insert(payload, cancellationToken: cancellationToken);
        if (response.ResponseMessage is null || !response.ResponseMessage.IsSuccessStatusCode)
        {
            var reason = response.ResponseMessage?.ReasonPhrase ?? "Unknown error";
            var errorMessage = "No error message provided.";
            if (response.ResponseMessage?.Content is not null)
            {
                errorMessage = await response.ResponseMessage.Content.ReadAsStringAsync(cancellationToken);
            }
            _logger.LogError(
                "Supabase insert failed for {CandidateId}. Reason: {Reason}. Error: {Error}",
                candidate.Id,
                reason,
                errorMessage);
            throw new InvalidOperationException($"Supabase insert failed: {reason}. {errorMessage}");
        }

        var statusCode = (int)response.ResponseMessage!.StatusCode;
        _logger.LogInformation("Inserted incident row for {CandidateId}. Status {StatusCode}.", candidate.Id, statusCode);
        return payload;
    }

    private async Task<FireIncident> BuildPayloadAsync(RssItemCandidate candidate, CancellationToken cancellationToken)
    {
        var when = candidate.PublishedAt ?? DateTimeOffset.UtcNow;
        var details = await _articleDetailsFetcher.FetchAsync(candidate, cancellationToken);
        var photoUrl = ResolvePhotoUrl(candidate, details);
        var street = ResolveStreet(candidate, details);

        return new FireIncident
        {
            Datetime = when.UtcDateTime,
            PhotoUrl = photoUrl,
            Street = street
        };
    }

    private string ResolvePhotoUrl(RssItemCandidate candidate, ArticleDetails details)
    {
        if (!string.IsNullOrWhiteSpace(_options.CurrentValue.DefaultPhotoUrl))
        {
            return _options.CurrentValue.DefaultPhotoUrl!;
        }

        if (!string.IsNullOrWhiteSpace(details.PhotoUrl))
        {
            return details.PhotoUrl!;
        }

        return string.IsNullOrWhiteSpace(candidate.Link) ? "" : candidate.Link;
    }

    private string ResolveStreet(RssItemCandidate candidate, ArticleDetails details)
    {
        if (!string.IsNullOrWhiteSpace(_options.CurrentValue.DefaultStreet))
        {
            return _options.CurrentValue.DefaultStreet!;
        }

        if (!string.IsNullOrWhiteSpace(details.Street))
        {
            return details.Street!;
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
                var options = new Supabase.SupabaseOptions
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

    private static bool TryGetConnectionInfo(SupabaseOptions options, out string url, out string key)
    {
        url = "";
        key = "";

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = options.ConnectionString
            };

            if (TryGetBuilderValue(builder, "Url", out url) &&
                (TryGetBuilderValue(builder, "ServiceRoleKey", out key) || TryGetBuilderValue(builder, "Key", out key)))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(options.Url) && !string.IsNullOrWhiteSpace(options.ServiceRoleKey))
        {
            url = options.Url;
            key = options.ServiceRoleKey;
            return true;
        }

        return false;
    }

    private static bool TryGetBuilderValue(DbConnectionStringBuilder builder, string key, out string value)
    {
        if (builder.TryGetValue(key, out var raw) && raw is not null)
        {
            value = raw.ToString() ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }

        value = "";
        return false;
    }
}
