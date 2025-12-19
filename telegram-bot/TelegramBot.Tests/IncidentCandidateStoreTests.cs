using TelegramBot.Models;
using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class IncidentCandidateStoreTests
{
    [Fact]
    public void TryAdd_DeduplicatesById()
    {
        var store = new IncidentCandidateStore();
        var first = new RssItemCandidate("abc", "title", "link", DateTimeOffset.UtcNow, null);
        var second = new RssItemCandidate("ABC", "title", "link", DateTimeOffset.UtcNow, null);

        var addedFirst = store.TryAdd(first);
        var addedSecond = store.TryAdd(second);

        Assert.True(addedFirst);
        Assert.False(addedSecond);
        Assert.Single(store.GetAll());
    }
}
