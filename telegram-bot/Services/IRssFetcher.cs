using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IRssFetcher
{
    Task<IReadOnlyCollection<RssItemCandidate>> FetchCandidatesAsync(CancellationToken cancellationToken);
}
