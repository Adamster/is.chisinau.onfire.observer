using Microsoft.Extensions.Logging.Abstractions;
using TelegramBot.Models;
using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class ApprovalProcessingServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsApprovedCandidates()
    {
        var store = new IncidentCandidateStore();
        var candidate = new RssItemCandidate("id-1", "Fire", "https://example.com/1", DateTimeOffset.UtcNow, null);
        store.TryAdd(candidate);
        store.TrySetDecision("id-1", ApprovalDecision.Approved);

        var repository = new StubIncidentRepository();
        var options = new TestOptionsMonitor<SupabaseOptions>(new SupabaseOptions
        {
            Url = "https://example.supabase.co",
            ServiceRoleKey = "service-role-key",
            PollIntervalSeconds = 30
        });

        var service = new ApprovalProcessingService(store, repository, options, NullLogger<ApprovalProcessingService>.Instance);
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await repository.WaitForInsertAsync();
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        Assert.True(store.GetAll().Single().IsPersisted);
    }

    private sealed class StubIncidentRepository : IIncidentRepository
    {
        private readonly TaskCompletionSource _called = new();

        public Task WaitForInsertAsync() => _called.Task;

        public Task AddIncidentAsync(RssItemCandidate candidate, CancellationToken cancellationToken)
        {
            _called.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
