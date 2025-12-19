using System.Collections.Concurrent;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class IncidentCandidateStore
{
    private readonly ConcurrentDictionary<string, RssItemCandidate> _candidates = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(RssItemCandidate candidate) => _candidates.TryAdd(candidate.Id, candidate);

    public IReadOnlyCollection<RssItemCandidate> GetAll() => _candidates.Values.ToList();
}
