namespace Kahoot.Application.IntegrationTests.TestSupport;

public sealed record TestCaller(Guid? UserId, string? Role, bool IsAuthenticated)
{
    public static TestCaller Anonymous { get; } = new(null, null, false);

    public static TestCaller Host(Guid userId) => new(userId, "Host", true);

    public static TestCaller SystemAdmin(Guid userId) => new(userId, "SystemAdmin", true);

    public static TestCaller Admin(Guid userId) => SystemAdmin(userId);
}
