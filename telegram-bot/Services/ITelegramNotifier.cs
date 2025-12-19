using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITelegramNotifier
{
    Task<int?> SendCandidateAsync(RssItemCandidate candidate, string callbackToken, CancellationToken cancellationToken);

    Task<int?> SendMessageAsync(string chatId, string message, CancellationToken cancellationToken);

    Task<int?> SendStreetSelectionAsync(
        string chatId,
        string prompt,
        IReadOnlyList<string> streets,
        string callbackToken,
        CancellationToken cancellationToken);

    Task AnswerCallbackAsync(string callbackQueryId, string message, bool showAlert, CancellationToken cancellationToken);

    Task RemoveInlineKeyboardAsync(long chatId, int messageId, CancellationToken cancellationToken);

    Task UpdateMessageTextAsync(long chatId, int messageId, string message, CancellationToken cancellationToken);
}
