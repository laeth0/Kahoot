namespace Kahoot.Application.IntegrationTests.TestSupport;

using Kahoot.Application.Common.Interfaces;

public sealed class TestCurrentUser : ICurrentUser
{
    public TestCurrentUser(TestCaller caller)
    {
        UserId = caller.UserId;
        Role = caller.Role;
        IsAuthenticated = caller.IsAuthenticated;
    }

    public Guid? UserId { get; }

    public string? Role { get; }

    public bool IsAuthenticated { get; }
}
