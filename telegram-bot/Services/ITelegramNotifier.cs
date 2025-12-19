using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITelegramNotifier
{
    Task<int?> SendCandidateAsync(RssItemCandidate candidate, CancellationToken cancellationToken);
}
