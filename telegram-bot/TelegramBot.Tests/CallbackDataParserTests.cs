using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class CallbackDataParserTests
{
    [Theory]
    [InlineData("approve:abc", ApprovalAction.Approve, "abc")]
    [InlineData("reject:xyz", ApprovalAction.Reject, "xyz")]
    public void TryParse_ReturnsActionAndId(string data, ApprovalAction expectedAction, string expectedId)
    {
        var parsed = CallbackDataParser.TryParse(data, out var action, out var candidateId);

        Assert.True(parsed);
        Assert.Equal(expectedAction, action);
        Assert.Equal(expectedId, candidateId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("approve:")]
    [InlineData("unknown:123")]
    public void TryParse_ReturnsFalseForInvalidData(string data)
    {
        var parsed = CallbackDataParser.TryParse(data, out _, out _);

        Assert.False(parsed);
    }
}
