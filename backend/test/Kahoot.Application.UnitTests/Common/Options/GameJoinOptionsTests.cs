using Kahoot.Application.Common.Options;
using Xunit;

namespace Kahoot.Application.UnitTests.Common.Options;

public sealed class GameJoinOptionsTests
{
    [Theory]
    [InlineData("https://kahoot.app")]
    [InlineData("https://game.kahoot.app")]
    [InlineData("https://kahoot.app:8443")]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("http://[::1]")]
    [InlineData("http://[::1]:8080")]
    public void HasValidOrigin_WithPermittedOrigins_ReturnsTrue(string clientBaseUrl)
    {
        GameJoinOptions options = new()
        {
            ClientBaseUrl = clientBaseUrl
        };

        bool isValid = GameJoinOptions.HasValidOrigin(options);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("kahoot.app")]
    [InlineData("https://kahoot.app/")]
    [InlineData(" https://kahoot.app")]
    [InlineData("https://kahoot.app ")]
    [InlineData("https://kahoot.app/join")]
    [InlineData("https://kahoot.app/games/lobby")]
    [InlineData("https://kahoot.app?pin=123456")]
    [InlineData("https://kahoot.app#section")]
    [InlineData("https://user:password@kahoot.app")]
    [InlineData("http://insecure-domain.com")]
    [InlineData("http://192.168.1.50:3000")]
    [InlineData("ftp://kahoot.app")]
    public void HasValidOrigin_WithRejectedOrigins_ReturnsFalse(string clientBaseUrl)
    {
        GameJoinOptions options = new()
        {
            ClientBaseUrl = clientBaseUrl
        };

        bool isValid = GameJoinOptions.HasValidOrigin(options);

        Assert.False(isValid);
    }
}
