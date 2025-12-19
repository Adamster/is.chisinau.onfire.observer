namespace TelegramBot.Models;

public sealed record ArticleDetails(string? PhotoUrl, string? Street, IReadOnlyList<string> Streets)
{
    public static readonly ArticleDetails Empty = new(null, null, Array.Empty<string>());
}
