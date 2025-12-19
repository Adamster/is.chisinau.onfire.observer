using Microsoft.Extensions.Logging.Abstractions;
using TelegramBot.Models;
using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class RssPollingServiceTests
{
    [Fact]
    public async Task ExecuteAsync_AddsFetchedCandidates()
    {
        var store = new IncidentCandidateStore();
        var fetcher = new StubRssFetcher(new[]
        {
            new RssItemCandidate("item-1", "Fire", "https://example.com/1", DateTimeOffset.UtcNow, null)
        });
        var notifier = new StubTelegramNotifier();
        var options = new TestOptionsMonitor<RssOptions>(new RssOptions
        {
            FeedUrl = "https://example.com/rss",
            PollIntervalSeconds = 30
        });

        var service = new RssPollingService(fetcher, store, notifier, options, NullLogger<RssPollingService>.Instance);
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await fetcher.WaitForCallAsync();
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        Assert.Single(store.GetAll());
    }

    private sealed class StubRssFetcher : IRssFetcher
    {
        private readonly IReadOnlyCollection<RssItemCandidate> _candidates;
        private readonly TaskCompletionSource _called = new();

        public StubRssFetcher(IReadOnlyCollection<RssItemCandidate> candidates)
        {
            _candidates = candidates;
        }

        public Task WaitForCallAsync() => _called.Task;

        public Task<IReadOnlyCollection<RssItemCandidate>> FetchCandidatesAsync(CancellationToken cancellationToken)
        {
            _called.TrySetResult();
            return Task.FromResult(_candidates);
        }
    }

    private sealed class StubTelegramNotifier : ITelegramNotifier
    {
        public Task<int?> SendCandidateAsync(RssItemCandidate candidate, string callbackToken, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);

        public Task<int?> SendMessageAsync(string chatId, string message, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);

        public Task<int?> SendStreetSelectionAsync(
            string chatId,
            string prompt,
            IReadOnlyList<string> streets,
            string callbackToken,
            CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);

        public Task AnswerCallbackAsync(
            string callbackQueryId,
            string message,
            bool showAlert,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveInlineKeyboardAsync(long chatId, int messageId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateMessageTextAsync(long chatId, int messageId, string message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
