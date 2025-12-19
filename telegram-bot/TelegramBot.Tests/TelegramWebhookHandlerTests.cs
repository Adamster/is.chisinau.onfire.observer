using Microsoft.Extensions.Logging.Abstractions;
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

        var options = new TestOptionsMonitor<TelegramBotOptions>(new TelegramBotOptions
        {
            ChatId = "123"
        });

        var handler = new TelegramWebhookHandler(store, options, NullLogger<TelegramWebhookHandler>.Instance);
        var update = new TelegramUpdate
        {
            UpdateId = 1,
            CallbackQuery = new TelegramCallbackQuery
            {
                Data = "approve:item-1",
                Message = new TelegramMessage
                {
                    MessageId = 42,
                    Chat = new TelegramChat
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

        var options = new TestOptionsMonitor<TelegramBotOptions>(new TelegramBotOptions
        {
            ChatId = "999"
        });

        var handler = new TelegramWebhookHandler(store, options, NullLogger<TelegramWebhookHandler>.Instance);
        var update = new TelegramUpdate
        {
            UpdateId = 1,
            CallbackQuery = new TelegramCallbackQuery
            {
                Data = "approve:item-1",
                Message = new TelegramMessage
                {
                    MessageId = 42,
                    Chat = new TelegramChat
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
}
