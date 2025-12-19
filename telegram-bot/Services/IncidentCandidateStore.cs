using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class IncidentCandidateStore
{
    private readonly ConcurrentDictionary<string, PendingIncident> _candidates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _candidateTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _callbackTokens = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(RssItemCandidate candidate)
    {
        var pending = new PendingIncident(candidate);
        if (!_candidates.TryAdd(candidate.Id, pending))
        {
            return false;
        }

        var token = GenerateToken(candidate.Id);
        if (!_callbackTokens.TryAdd(token, candidate.Id))
        {
            token = GenerateToken($"{candidate.Id}:{Guid.NewGuid():N}");
            if (!_callbackTokens.TryAdd(token, candidate.Id))
            {
                _candidates.TryRemove(candidate.Id, out _);
                return false;
            }
        }

        _candidateTokens[candidate.Id] = token;
        return true;
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

    public bool TryBeginPersisting(string candidateId)
    {
        if (_candidates.TryGetValue(candidateId, out var pending))
        {
            return pending.TryBeginPersisting();
        }

        return false;
    }

    public void CancelPersisting(string candidateId)
    {
        if (_candidates.TryGetValue(candidateId, out var pending))
        {
            pending.CancelPersisting();
        }
    }

    public bool TryGetCandidate(string candidateId, out PendingIncident? pending) =>
        _candidates.TryGetValue(candidateId, out pending);

    public bool TryGetCallbackToken(string candidateId, out string? token) =>
        _candidateTokens.TryGetValue(candidateId, out token);

    public bool TryGetCandidateId(string callbackToken, out string? candidateId) =>
        _callbackTokens.TryGetValue(callbackToken, out candidateId);

    public IReadOnlyCollection<PendingIncident> GetAll() => _candidates.Values.ToList();

    private static string GenerateToken(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }
}
