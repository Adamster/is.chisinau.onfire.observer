namespace TelegramBot.Models;

public sealed class PendingIncident
{
    public PendingIncident(RssItemCandidate candidate)
    {
        Candidate = candidate;
    }

    public RssItemCandidate Candidate { get; }

    public ApprovalDecision Decision { get; private set; } = ApprovalDecision.Pending;

    public int? TelegramMessageId { get; private set; }

    public bool IsPersisted { get; private set; }

    public bool IsPersisting { get; private set; }

    public void MarkNotified(int messageId)
    {
        TelegramMessageId = messageId;
    }

    public bool TrySetDecision(ApprovalDecision decision)
    {
        if (Decision != ApprovalDecision.Pending)
        {
            return false;
        }

        Decision = decision;
        return true;
    }

    public bool TryMarkPersisted()
    {
        if (IsPersisted)
        {
            return false;
        }

        IsPersisted = true;
        IsPersisting = false;
        return true;
    }

    public bool TryBeginPersisting()
    {
        if (IsPersisted || IsPersisting)
        {
            return false;
        }

        IsPersisting = true;
        return true;
    }

    public void CancelPersisting()
    {
        if (!IsPersisted)
        {
            IsPersisting = false;
        }
    }
}
