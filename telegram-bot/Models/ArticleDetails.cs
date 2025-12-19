namespace TelegramBot.Models;

public sealed record ArticleDetails(string? PhotoUrl, string? Street)
{
    public static readonly ArticleDetails Empty = new(null, null);
}
