using Microsoft.Extensions.Options;
using Npgsql;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class SupabaseIncidentRepository : IIncidentRepository
{
    private readonly IOptionsMonitor<SupabaseOptions> _options;
    private readonly ILogger<SupabaseIncidentRepository> _logger;

    public SupabaseIncidentRepository(IOptionsMonitor<SupabaseOptions> options, ILogger<SupabaseIncidentRepository> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task AddIncidentAsync(RssItemCandidate candidate, CancellationToken cancellationToken)
    {
        var connectionString = _options.CurrentValue.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Supabase connection string is not configured.");
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            insert into public.fire_incidents (datetime, photo_url, street)
            values (@datetime, @photo_url, @street);
            """;

        var when = candidate.PublishedAt ?? DateTimeOffset.UtcNow;
        var photoUrl = ResolvePhotoUrl(candidate);
        var street = ResolveStreet(candidate);

        command.Parameters.AddWithValue("datetime", when.UtcDateTime);
        command.Parameters.AddWithValue("photo_url", photoUrl);
        command.Parameters.AddWithValue("street", street);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Inserted {Rows} incident rows for {CandidateId}.", rows, candidate.Id);
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
}
