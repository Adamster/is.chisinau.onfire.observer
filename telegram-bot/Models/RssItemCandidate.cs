namespace TelegramBot.Models;

public sealed record RssItemCandidate(
    string Id,
    string Title,
    string Link,
    DateTimeOffset? PublishedAt,
    string? Summary);
