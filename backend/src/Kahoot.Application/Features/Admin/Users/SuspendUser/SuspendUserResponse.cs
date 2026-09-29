namespace Kahoot.Application.Features.Admin.Users.SuspendUser;

public sealed record SuspendUserResponse(
    // Two-Phase Asynchronous Marker - True indicates Phase 2 game finalization worker task is running in background (ACCT-SUSP-002, ACCT-SUSP-004)
    bool TerminationPending);
