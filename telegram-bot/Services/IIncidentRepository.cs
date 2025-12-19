using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IIncidentRepository
{
    Task AddIncidentAsync(RssItemCandidate candidate, CancellationToken cancellationToken);
}
