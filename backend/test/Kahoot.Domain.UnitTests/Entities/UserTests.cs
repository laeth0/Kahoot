using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Xunit;

namespace Kahoot.Domain.UnitTests.Entities;

public sealed class UserTests
{
    [Fact]
    public void NewUser_DefaultsToHostRole()
    {
        User user = CreateUser();

        Assert.Equal(UserRole.Host, user.Role);
    }

    [Fact]
    public void NewUser_DefaultsToActiveStatus()
    {
        User user = CreateUser();

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void NewUser_StartsWithInitialTokenSecurityVersion()
    {
        User user = CreateUser();

        Assert.Equal(1, user.TokenSecurityVersion);
    }

    [Fact]
    public void NewUser_StartsWithInitialRevision()
    {
        User user = CreateUser();

        Assert.Equal(1L, user.Revision);
    }

    private static User CreateUser() => new()
    {
        DisplayUsername = "Host",
        NormalizedUsername = "HOST",
        PasswordHash = "hash-not-used-by-domain-tests"
    };
}
