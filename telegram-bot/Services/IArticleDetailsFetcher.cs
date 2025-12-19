using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IArticleDetailsFetcher
{
    Task<ArticleDetails> FetchAsync(RssItemCandidate candidate, CancellationToken cancellationToken);
}
