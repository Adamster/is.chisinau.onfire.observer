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

    public IReadOnlyList<string> StreetOptions { get; private set; } = Array.Empty<string>();

    public string? SelectedStreet { get; private set; }

    public bool IsAwaitingManualStreet { get; private set; }

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

    public bool TrySetStreetOptions(IReadOnlyList<string> options)
    {
        if (options is null || options.Count == 0)
        {
            return false;
        }

        StreetOptions = options;
        return true;
    }

    public bool TrySelectStreet(string street)
    {
        if (Decision != ApprovalDecision.Approved)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(street))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SelectedStreet))
        {
            return false;
        }

        SelectedStreet = street;
        return true;
    }

    public bool TryBeginManualStreet()
    {
        if (Decision != ApprovalDecision.Approved)
        {
            return false;
        }

        if (IsAwaitingManualStreet || !string.IsNullOrWhiteSpace(SelectedStreet))
        {
            return false;
        }

        IsAwaitingManualStreet = true;
        return true;
    }

    public bool TrySelectManualStreet(string street)
    {
        if (!IsAwaitingManualStreet)
        {
            return false;
        }

        if (!TrySelectStreet(street))
        {
            if (!string.IsNullOrWhiteSpace(SelectedStreet))
            {
                IsAwaitingManualStreet = false;
            }

            return false;
        }

        IsAwaitingManualStreet = false;
        return true;
    }

    public void CancelManualStreet()
    {
        if (string.IsNullOrWhiteSpace(SelectedStreet))
        {
            IsAwaitingManualStreet = false;
        }
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
