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
                  <?xml version=\"1.0\" encoding=\"utf-8\"?>
                  <rss version=\"2.0\">
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
            FeedUrl = "https://example.com/rss",
            Keywords = new List<string> { "fire" }
        });

        var fetcher = new RssFetcher(client, options, NullLogger<RssFetcher>.Instance);
        var results = await fetcher.FetchCandidatesAsync(CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("fire-1", results.First().Id);
    }

    [Fact]
    public async Task FetchCandidatesAsync_ReturnsEmptyWhenFeedUrlMissing()
    {
        var handler = new StubHttpMessageHandler(string.Empty, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var options = new TestOptionsMonitor<RssOptions>(new RssOptions
        {
            FeedUrl = string.Empty
        });

        var fetcher = new RssFetcher(client, options, NullLogger<RssFetcher>.Instance);
        var results = await fetcher.FetchCandidatesAsync(CancellationToken.None);

        Assert.Empty(results);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string content, HttpStatusCode statusCode)
        {
            _content = content;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/rss+xml")
            };

            return Task.FromResult(response);
        }
    }
}
