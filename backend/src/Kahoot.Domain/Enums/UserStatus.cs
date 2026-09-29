namespace Kahoot.Domain.Enums;

public enum UserStatus
{
    // Active State - Account is in good standing with full operational privileges
    Active = 1,

    // Suspended State - Account is administratively blocked; active games cancelled and tokens invalidated
    Suspended = 2
}
