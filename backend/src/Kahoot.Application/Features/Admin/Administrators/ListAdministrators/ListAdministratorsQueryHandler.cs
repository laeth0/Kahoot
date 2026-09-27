using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Admin.Administrators.ListAdministrators;

public sealed class ListAdministratorsQueryHandler : IQueryHandler<ListAdministratorsQuery, IReadOnlyList<AdministratorResponse>>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ListAdministratorsQueryHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<AdministratorResponse>>> Handle(
        ListAdministratorsQuery query,
        CancellationToken cancellationToken)
    {
        // State: Caller authorization check (must be an authenticated SystemAdmin)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure<IReadOnlyList<AdministratorResponse>>(AuthErrors.Forbidden);
        }

        // Step: Query all SystemAdmin accounts ordered by creation timestamp
        List<AdministratorResponse> administrators = await _dbContext.Users
            .AsNoTracking()
            .TagWith("Admin:ListAdministrators")
            .Where(user => user.Role == UserRole.SystemAdmin)
            .OrderBy(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Select(user => new AdministratorResponse(
                user.Id,
                user.DisplayUsername,
                user.Role,
                user.Status,
                user.CreatedAt,
                user.Status == UserStatus.Suspended ? (DateTimeOffset?)user.UpdatedAt : null,
                user.Revision,
                user.TerminationPending))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AdministratorResponse>>(administrators);
    }
}
