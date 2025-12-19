using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class ArticleDetailsFetcher : IArticleDetailsFetcher
{
    private static readonly Regex StreetRegex = new(
        @"\b(?:Strada|strada|Str\.|str\.|Bulevardul|Bulevard|bd\.|bd|Bul\.|bul\.|Aleea|Șoseaua|Soseaua|Prospectul|ул\.|улица|проспект|пр-т)\s+[A-ZĂÂÎȘȚ][^,\n]{2,60}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ArticleDetailsFetcher> _logger;

    public ArticleDetailsFetcher(HttpClient httpClient, ILogger<ArticleDetailsFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ArticleDetails> FetchAsync(RssItemCandidate candidate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(candidate.Link))
        {
            return ArticleDetails.Empty;
        }

        if (!Uri.TryCreate(candidate.Link, UriKind.Absolute, out var baseUri))
        {
            return ArticleDetails.Empty;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, baseUri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; TelegramBot/1.0)");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch article {Url}. Status code: {StatusCode}", baseUri, response.StatusCode);
                return ArticleDetails.Empty;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return ArticleDetails.Empty;
            }

            var document = new HtmlDocument();
            document.LoadHtml(html);

            var photoUrl = ResolvePhotoUrl(document, baseUri);
            var streets = ResolveStreets(document);
            var street = streets.FirstOrDefault();

            return new ArticleDetails(photoUrl, street, streets);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Unable to fetch article details for {Url}.", baseUri);
            return ArticleDetails.Empty;
        }
    }

    private static string? ResolvePhotoUrl(HtmlDocument document, Uri baseUri)
    {
        var metaPhoto = document.DocumentNode.SelectSingleNode("//meta[@property='og:image' or @name='og:image' or @property='twitter:image' or @name='twitter:image']");
        var photoUrl = metaPhoto?.GetAttributeValue("content", null);

        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            var articleImage = document.DocumentNode.SelectSingleNode("//article//img[@src]") ??
                               document.DocumentNode.SelectSingleNode("//img[@src]");
            photoUrl = articleImage?.GetAttributeValue("src", null) ?? articleImage?.GetAttributeValue("data-src", null);
        }

        return NormalizeUrl(baseUri, photoUrl);
    }

    private static string? NormalizeUrl(Uri baseUri, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            ? absolute.ToString()
            : new Uri(baseUri, url).ToString();
    }

    private static IReadOnlyList<string> ResolveStreets(HtmlDocument document)
    {
        var articleText = document.DocumentNode.SelectSingleNode("//article")?.InnerText ??
                          document.DocumentNode.SelectSingleNode("//body")?.InnerText ??
                          document.DocumentNode.InnerText;

        if (string.IsNullOrWhiteSpace(articleText))
        {
            return Array.Empty<string>();
        }

        var decoded = WebUtility.HtmlDecode(articleText);
        var normalized = Regex.Replace(decoded, @"\s+", " ").Trim();
        var matches = StreetRegex.Matches(normalized);
        if (matches.Count == 0)
        {
            return Array.Empty<string>();
        }

        var results = new List<string>();
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var value = match.Value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!results.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(value);
            }
        }

        return results;
    }
}
