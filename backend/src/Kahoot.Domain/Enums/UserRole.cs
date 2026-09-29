namespace Kahoot.Domain.Enums;

public enum UserRole
{
    // Host Tenant Role - Authoring quizzes, hosting live game sessions, and managing questions
    Host = 1,

    // System Administrator Role - Platform supervision, user lifecycle management, and system seeding
    SystemAdmin = 2
}
