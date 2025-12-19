using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITelegramNotifier
{
    Task<int?> SendCandidateAsync(RssItemCandidate candidate, string callbackToken, CancellationToken cancellationToken);

    Task<int?> SendMessageAsync(string chatId, string message, CancellationToken cancellationToken);

    Task AnswerCallbackAsync(string callbackQueryId, string message, CancellationToken cancellationToken);
}
