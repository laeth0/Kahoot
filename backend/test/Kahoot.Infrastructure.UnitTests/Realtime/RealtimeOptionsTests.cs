using Kahoot.Infrastructure.Realtime;
using Xunit;

namespace Kahoot.Infrastructure.UnitTests.Realtime;

public sealed class RealtimeOptionsTests
{
    [Theory]
    [InlineData("k")]
    [InlineData("1")]
    [InlineData("_")]
    [InlineData("-")]
    [InlineData("kahoot")]
    [InlineData("kahoot_games-cluster_01")]
    public void HasValidChannelPrefix_AcceptsAsciiIdentifier(string channelPrefix)
    {
        RealtimeOptions options = new()
        {
            ChannelPrefix = channelPrefix
        };

        bool isValid = RealtimeOptions.HasValidChannelPrefix(options);

        Assert.True(isValid);
    }

    [Fact]
    public void HasValidChannelPrefix_AcceptsMaxLengthSixtyFour()
    {
        string maxValid = new('a', 64);
        RealtimeOptions options = new()
        {
            ChannelPrefix = maxValid
        };

        bool isValid = RealtimeOptions.HasValidChannelPrefix(options);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kahoot game")]
    [InlineData(" kahoot")]
    [InlineData("kahoot ")]
    [InlineData("kahoot:game")]
    [InlineData("kahoot.game")]
    [InlineData("kahoot/game")]
    [InlineData("kahoot\ngame")]
    [InlineData("kahoot_é")]
    [InlineData("\u0661\u0662\u0663")]
    [InlineData("kahoot_🎉")]
    public void HasValidChannelPrefix_RejectsInvalidIdentifier(string invalidPrefix)
    {
        RealtimeOptions options = new()
        {
            ChannelPrefix = invalidPrefix
        };

        bool isValid = RealtimeOptions.HasValidChannelPrefix(options);

        Assert.False(isValid);
    }

    [Fact]
    public void HasValidChannelPrefix_RejectsLengthSixtyFive()
    {
        string tooLong = new('a', 65);
        RealtimeOptions options = new()
        {
            ChannelPrefix = tooLong
        };

        bool isValid = RealtimeOptions.HasValidChannelPrefix(options);

        Assert.False(isValid);
    }
}
