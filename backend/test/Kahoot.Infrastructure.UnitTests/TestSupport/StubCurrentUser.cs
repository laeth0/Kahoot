namespace Kahoot.Infrastructure.UnitTests.TestSupport;

using System;
using Kahoot.Application.Common.Interfaces;

public sealed class StubCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public bool IsAuthenticated => UserId.HasValue;

    public StubCurrentUser(Guid? userId = null, string? role = null)
    {
        UserId = userId;
        Role = role;
    }
}
