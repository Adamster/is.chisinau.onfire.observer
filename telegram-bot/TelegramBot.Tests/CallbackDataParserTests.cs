using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class CallbackDataParserTests
{
    [Theory]
    [InlineData("approve:abc", ApprovalAction.Approve, "abc")]
    [InlineData("reject:xyz", ApprovalAction.Reject, "xyz")]
    [InlineData("street:token:1", ApprovalAction.SelectStreet, "token")]
    public void TryParse_ReturnsActionAndId(string data, ApprovalAction expectedAction, string expectedId)
    {
        var parsed = CallbackDataParser.TryParse(data, out var action, out var candidateId, out _);

        Assert.True(parsed);
        Assert.Equal(expectedAction, action);
        Assert.Equal(expectedId, candidateId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("approve:")]
    [InlineData("street:token")]
    [InlineData("street:token:")]
    [InlineData("unknown:123")]
    public void TryParse_ReturnsFalseForInvalidData(string data)
    {
        var parsed = CallbackDataParser.TryParse(data, out _, out _, out _);

        Assert.False(parsed);
    }
}
