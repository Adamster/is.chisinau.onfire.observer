namespace TelegramBot.Services;

public static class CallbackDataParser
{
    private const string ApprovePrefix = "approve:";
    private const string RejectPrefix = "reject:";

    public static bool TryParse(string? data, out ApprovalAction action, out string? candidateId)
    {
        action = ApprovalAction.None;
        candidateId = null;

        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        if (data.StartsWith(ApprovePrefix, StringComparison.OrdinalIgnoreCase))
        {
            action = ApprovalAction.Approve;
            candidateId = data[ApprovePrefix.Length..];
            return !string.IsNullOrWhiteSpace(candidateId);
        }

        if (data.StartsWith(RejectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            action = ApprovalAction.Reject;
            candidateId = data[RejectPrefix.Length..];
            return !string.IsNullOrWhiteSpace(candidateId);
        }

        return false;
    }
}

public enum ApprovalAction
{
    None,
    Approve,
    Reject
}
