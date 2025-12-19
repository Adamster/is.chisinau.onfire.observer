using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IIncidentRepository
{
    Task<FireIncident?> AddIncidentAsync(RssItemCandidate candidate, string? streetOverride, CancellationToken cancellationToken);
}
