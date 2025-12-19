using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Options;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class RssFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<RssOptions> _options;
    private readonly ILogger<RssFetcher> _logger;

    public RssFetcher(HttpClient httpClient, IOptionsMonitor<RssOptions> options, ILogger<RssFetcher> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<RssItemCandidate>> FetchCandidatesAsync(CancellationToken cancellationToken)
    {
        var feedUrl = _options.CurrentValue.FeedUrl;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            _logger.LogWarning("RSS feed URL is not configured.");
            return Array.Empty<RssItemCandidate>();
        }

        using var response = await _httpClient.GetAsync(feedUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        var feed = SyndicationFeed.Load(xmlReader);

        if (feed is null)
        {
            _logger.LogWarning("RSS feed returned no items.");
            return Array.Empty<RssItemCandidate>();
        }

        var keywords = _options.CurrentValue.Keywords;
        var normalizedKeywords = keywords.Count == 0
            ? Array.Empty<string>()
            : keywords.Select(keyword => keyword.Trim()).Where(keyword => keyword.Length > 0).ToArray();

        var candidates = new List<RssItemCandidate>();
        foreach (var item in feed.Items)
        {
            var title = item.Title?.Text ?? string.Empty;
            var summary = item.Summary?.Text;
            var link = item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty;
            var content = string.Join(' ', new[] { title, summary }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (!MatchesKeywords(content, normalizedKeywords))
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(item.Id) ? link : item.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogDebug("Skipping RSS item without id or link: {Title}", title);
                continue;
            }

            candidates.Add(new RssItemCandidate(
                id,
                title,
                link,
                item.PublishDate == DateTimeOffset.MinValue ? null : item.PublishDate,
                summary));
        }

        return candidates;
    }

    private static bool MatchesKeywords(string content, IReadOnlyCollection<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
