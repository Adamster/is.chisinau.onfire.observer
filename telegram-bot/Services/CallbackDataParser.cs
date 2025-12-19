namespace TelegramBot.Services;

public static class CallbackDataParser
{
    private const string ApprovePrefix = "approve:";
    private const string RejectPrefix = "reject:";
    private const string StreetPrefix = "street:";

    public static bool TryParse(string? data, out ApprovalAction action, out string? token, out string? payload)
    {
        action = ApprovalAction.None;
        token = null;
        payload = null;

        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        if (data.StartsWith(ApprovePrefix, StringComparison.OrdinalIgnoreCase))
        {
            action = ApprovalAction.Approve;
            token = data[ApprovePrefix.Length..];
            return !string.IsNullOrWhiteSpace(token);
        }

        if (data.StartsWith(RejectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            action = ApprovalAction.Reject;
            token = data[RejectPrefix.Length..];
            return !string.IsNullOrWhiteSpace(token);
        }

        if (data.StartsWith(StreetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = data[StreetPrefix.Length..];
            var separatorIndex = remainder.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == remainder.Length - 1)
            {
                return false;
            }

            action = ApprovalAction.SelectStreet;
            token = remainder[..separatorIndex];
            payload = remainder[(separatorIndex + 1)..];
            return !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(payload);
        }

        return false;
    }
}

public enum ApprovalAction
{
    None,
    Approve,
    Reject,
    SelectStreet
}
