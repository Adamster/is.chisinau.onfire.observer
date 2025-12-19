using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Models;
using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class TelegramWebhookHandlerTests
{
    [Fact]
    public async Task HandleUpdate_UpdatesDecisionWhenAuthorized()
    {
        var store = new IncidentCandidateStore();
        store.TryAdd(new RssItemCandidate("item-1", "Title", "https://example.com", DateTimeOffset.UtcNow, null));
        Assert.True(store.TryGetCallbackToken("item-1", out var callbackToken));

        var notifier = new StubTelegramNotifier();
        var repository = new StubIncidentRepository();
        var detailsFetcher = new StubArticleDetailsFetcher();
        var options = new TestOptionsMonitor<TelegramBotOptions>(new TelegramBotOptions
        {
            ChatId = "123"
        });
        var rssOptions = new TestOptionsMonitor<RssOptions>(new RssOptions());
        var supabaseOptions = new TestOptionsMonitor<SupabaseOptions>(new SupabaseOptions());

        var handler = new TelegramWebhookHandler(
            store,
            notifier,
            repository,
            detailsFetcher,
            options,
            rssOptions,
            supabaseOptions,
            NullLogger<TelegramWebhookHandler>.Instance);
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-1",
                Data = $"approve:{callbackToken}",
                Message = new Message
                {
                    Id = 42,
                    Date = DateTime.UtcNow,
                    Chat = new Chat
                    {
                        Id = 123,
                        Type = ChatType.Private
                    }
                }
            }
        };

        var handled = await handler.HandleUpdateAsync(update, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(ApprovalDecision.Approved, store.GetAll().Single().Decision);
    }

    [Fact]
    public async Task HandleUpdate_IgnoresUnauthorizedChat()
    {
        var store = new IncidentCandidateStore();
        store.TryAdd(new RssItemCandidate("item-1", "Title", "https://example.com", DateTimeOffset.UtcNow, null));
        Assert.True(store.TryGetCallbackToken("item-1", out var callbackToken));

        var notifier = new StubTelegramNotifier();
        var repository = new StubIncidentRepository();
        var detailsFetcher = new StubArticleDetailsFetcher();
        var options = new TestOptionsMonitor<TelegramBotOptions>(new TelegramBotOptions
        {
            ChatId = "999"
        });
        var rssOptions = new TestOptionsMonitor<RssOptions>(new RssOptions());
        var supabaseOptions = new TestOptionsMonitor<SupabaseOptions>(new SupabaseOptions());

        var handler = new TelegramWebhookHandler(
            store,
            notifier,
            repository,
            detailsFetcher,
            options,
            rssOptions,
            supabaseOptions,
            NullLogger<TelegramWebhookHandler>.Instance);
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-2",
                Data = $"approve:{callbackToken}",
                Message = new Message
                {
                    Id = 42,
                    Date = DateTime.UtcNow,
                    Chat = new Chat
                    {
                        Id = 123,
                        Type = ChatType.Private
                    }
                }
            }
        };

        var handled = await handler.HandleUpdateAsync(update, CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(ApprovalDecision.Pending, store.GetAll().Single().Decision);
    }

    [Fact]
    public async Task HandleUpdate_StreetSelectionInsertsCandidate()
    {
        var store = new IncidentCandidateStore();
        store.TryAdd(new RssItemCandidate("item-1", "Title", "https://example.com", DateTimeOffset.UtcNow, null));
        store.TrySetDecision("item-1", ApprovalDecision.Approved);
        store.TrySetStreetOptions("item-1", new[] { "Strada Test" });
        Assert.True(store.TryGetCallbackToken("item-1", out var callbackToken));

        var notifier = new StubTelegramNotifier();
        var repository = new StubIncidentRepository();
        var detailsFetcher = new StubArticleDetailsFetcher();
        var options = new TestOptionsMonitor<TelegramBotOptions>(new TelegramBotOptions
        {
            ChatId = "123"
        });
        var rssOptions = new TestOptionsMonitor<RssOptions>(new RssOptions());
        var supabaseOptions = new TestOptionsMonitor<SupabaseOptions>(new SupabaseOptions());

        var handler = new TelegramWebhookHandler(
            store,
            notifier,
            repository,
            detailsFetcher,
            options,
            rssOptions,
            supabaseOptions,
            NullLogger<TelegramWebhookHandler>.Instance);
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-3",
                Data = $"street:{callbackToken}:0",
                Message = new Message
                {
                    Id = 42,
                    Date = DateTime.UtcNow,
                    Chat = new Chat
                    {
                        Id = 123,
                        Type = ChatType.Private
                    }
                }
            }
        };

        var handled = await handler.HandleUpdateAsync(update, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal("Strada Test", repository.LastStreetOverride);
        Assert.Equal(1, repository.CallCount);
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

    private sealed class StubIncidentRepository : IIncidentRepository
    {
        public int CallCount { get; private set; }

        public string? LastStreetOverride { get; private set; }

        public Task<FireIncident?> AddIncidentAsync(
            RssItemCandidate candidate,
            string? streetOverride,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastStreetOverride = streetOverride;
            var incident = new FireIncident
            {
                Datetime = (candidate.PublishedAt ?? DateTimeOffset.UtcNow).UtcDateTime,
                PhotoUrl = candidate.Link,
                Street = streetOverride ?? candidate.Title
            };

            return Task.FromResult<FireIncident?>(incident);
        }
    }

    private sealed class StubArticleDetailsFetcher : IArticleDetailsFetcher
    {
        public Task<ArticleDetails> FetchAsync(RssItemCandidate candidate, CancellationToken cancellationToken) =>
            Task.FromResult(new ArticleDetails(candidate.Link, "Strada Test", new[] { "Strada Test" }));
    }
}
