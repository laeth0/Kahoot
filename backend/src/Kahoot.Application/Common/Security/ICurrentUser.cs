namespace Kahoot.Application.Common.Security;

public interface ICurrentUser
{
    Guid? HostId { get; }

    bool IsAuthenticated { get; }
}
