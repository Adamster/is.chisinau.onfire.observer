using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot.Types;
using TelegramBot.Models;
using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class TelegramWebhookHandlerTests
{
    [Fact]
    public void HandleUpdate_UpdatesDecisionWhenAuthorized()
    {
        var store = new IncidentCandidateStore();
        store.TryAdd(new RssItemCandidate("item-1", "Title", "https://example.com", DateTimeOffset.UtcNow, null));

        var notifier = new StubTelegramNotifier();
        var options = new TestOptionsMonitor<TelegramBotOptions>(new TelegramBotOptions
        {
            ChatId = "123"
        });
        var rssOptions = new TestOptionsMonitor<RssOptions>(new RssOptions());
        var supabaseOptions = new TestOptionsMonitor<SupabaseOptions>(new SupabaseOptions());

        var handler = new TelegramWebhookHandler(
            store,
            notifier,
            options,
            rssOptions,
            supabaseOptions,
            NullLogger<TelegramWebhookHandler>.Instance);
        var update = new Update
        {
            UpdateId = 1,
            CallbackQuery = new CallbackQuery
            {
                Data = "approve:item-1",
                Message = new Message
                {
                    MessageId = 42,
                    Chat = new Chat
                    {
                        Id = 123
                    }
                }
            }
        };

        var handled = handler.HandleUpdate(update);

        Assert.True(handled);
        Assert.Equal(ApprovalDecision.Approved, store.GetAll().Single().Decision);
    }

    [Fact]
    public void HandleUpdate_IgnoresUnauthorizedChat()
    {
        var store = new IncidentCandidateStore();
        store.TryAdd(new RssItemCandidate("item-1", "Title", "https://example.com", DateTimeOffset.UtcNow, null));

        var notifier = new StubTelegramNotifier();
        var options = new TestOptionsMonitor<TelegramBotOptions>(new TelegramBotOptions
        {
            ChatId = "999"
        });
        var rssOptions = new TestOptionsMonitor<RssOptions>(new RssOptions());
        var supabaseOptions = new TestOptionsMonitor<SupabaseOptions>(new SupabaseOptions());

        var handler = new TelegramWebhookHandler(
            store,
            notifier,
            options,
            rssOptions,
            supabaseOptions,
            NullLogger<TelegramWebhookHandler>.Instance);
        var update = new Update
        {
            UpdateId = 1,
            CallbackQuery = new CallbackQuery
            {
                Data = "approve:item-1",
                Message = new Message
                {
                    MessageId = 42,
                    Chat = new Chat
                    {
                        Id = 123
                    }
                }
            }
        };

        var handled = handler.HandleUpdate(update);

        Assert.False(handled);
        Assert.Equal(ApprovalDecision.Pending, store.GetAll().Single().Decision);
    }

    private sealed class StubTelegramNotifier : ITelegramNotifier
    {
        public Task<int?> SendCandidateAsync(RssItemCandidate candidate, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);

        public Task<int?> SendMessageAsync(string chatId, string message, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);
    }
}
