using System.Collections.Concurrent;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class IncidentCandidateStore
{
    private readonly ConcurrentDictionary<string, PendingIncident> _candidates = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(RssItemCandidate candidate)
    {
        var pending = new PendingIncident(candidate);
        return _candidates.TryAdd(candidate.Id, pending);
    }

    public bool TryMarkNotified(string candidateId, int messageId)
    {
        if (_candidates.TryGetValue(candidateId, out var pending))
        {
            pending.MarkNotified(messageId);
            return true;
        }

        return false;
    }

    public bool TrySetDecision(string candidateId, ApprovalDecision decision)
    {
        if (_candidates.TryGetValue(candidateId, out var pending))
        {
            return pending.TrySetDecision(decision);
        }

        return false;
    }

    public bool TryMarkPersisted(string candidateId)
    {
        if (_candidates.TryGetValue(candidateId, out var pending))
        {
            return pending.TryMarkPersisted();
        }

        return false;
    }

    public IReadOnlyCollection<PendingIncident> GetAll() => _candidates.Values.ToList();
}
