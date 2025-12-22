using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class RssFetcherTests
{
    [Fact]
    public async Task FetchCandidatesAsync_FiltersByKeyword()
    {
        var rss = """
                  <?xml version="1.0" encoding="utf-8"?>
                  <rss version="2.0">
                    <channel>
                      <title>Test Feed</title>
                      <item>
                        <title>Fire in Chisinau</title>
                        <link>https://example.com/fire</link>
                        <guid>fire-1</guid>
                        <description>Fire incident reported</description>
                      </item>
                      <item>
                        <title>Sports update</title>
                        <link>https://example.com/sports</link>
                        <guid>sports-1</guid>
                        <description>Sports news</description>
                      </item>
                    </channel>
                  </rss>
                  """;

        var handler = new StubHttpMessageHandler(rss, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var options = new TestOptionsMonitor<RssOptions>(new RssOptions
        {
            FeedUrls = new List<string> { "https://example.com/rss" },
            Keywords = new List<string> { "fire" }
        });

        var fetcher = new RssFetcher(client, options, NullLogger<RssFetcher>.Instance);
        var results = await fetcher.FetchCandidatesAsync(CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("fire-1", results.First().Id);
    }

    [Fact]
    public async Task FetchCandidatesAsync_ReturnsEmptyWhenFeedUrlsMissing()
    {
        var handler = new StubHttpMessageHandler(string.Empty, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var options = new TestOptionsMonitor<RssOptions>(new RssOptions
        {
            FeedUrls = new List<string>()
        });

        var fetcher = new RssFetcher(client, options, NullLogger<RssFetcher>.Instance);
        var results = await fetcher.FetchCandidatesAsync(CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FetchCandidatesAsync_CombinesMultipleFeeds()
    {
        var fireFeed = """
                       <?xml version="1.0" encoding="utf-8"?>
                       <rss version="2.0">
                         <channel>
                           <title>Feed One</title>
                           <item>
                             <title>Fire in town</title>
                             <link>https://example.com/fire</link>
                             <guid>fire-1</guid>
                             <description>Incident</description>
                           </item>
                         </channel>
                       </rss>
                       """;

        var secondFeed = """
                         <?xml version="1.0" encoding="utf-8"?>
                         <rss version="2.0">
                           <channel>
                             <title>Feed Two</title>
                             <item>
                               <title>Another fire report</title>
                               <link>https://example.com/fire-2</link>
                               <guid>fire-2</guid>
                               <description>Smoke spotted</description>
                             </item>
                           </channel>
                         </rss>
                         """;

        var handler = new StubHttpMessageHandler(new Dictionary<string, string>
        {
            ["https://example.com/one"] = fireFeed,
            ["https://example.com/two"] = secondFeed
        }, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var options = new TestOptionsMonitor<RssOptions>(new RssOptions
        {
            FeedUrls = new List<string> { "https://example.com/one", "https://example.com/two" },
            Keywords = new List<string> { "fire" }
        });

        var fetcher = new RssFetcher(client, options, NullLogger<RssFetcher>.Instance);
        var results = await fetcher.FetchCandidatesAsync(CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, candidate => candidate.Id == "fire-1");
        Assert.Contains(results, candidate => candidate.Id == "fire-2");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;
        private readonly IReadOnlyDictionary<string, string>? _responses;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string content, HttpStatusCode statusCode)
        {
            _content = content;
            _statusCode = statusCode;
        }

        public StubHttpMessageHandler(IReadOnlyDictionary<string, string> responses, HttpStatusCode statusCode)
        {
            _content = string.Empty;
            _responses = responses;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = _content;
            if (_responses is not null && request.RequestUri is not null)
            {
                if (!_responses.TryGetValue(request.RequestUri.ToString(), out content))
                {
                    content = string.Empty;
                }
            }

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/rss+xml")
            };

            return Task.FromResult(response);
        }
    }
}
