using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin;
using Kahoot.Application.Features.Admin.Administrators;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Admin.Administrators.CreateAdministrator;

public sealed class CreateAdministratorCommandHandler : ICommandHandler<CreateAdministratorCommand, AdministratorResponse>
{
    private const string NormalizedUsernameConstraintName = "ux_users_normalized_username";

    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public CreateAdministratorCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AdministratorResponse>> Handle(
        CreateAdministratorCommand request,
        CancellationToken cancellationToken)
    {
        // State: Caller authorization check (must be an authenticated SystemAdmin)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure<AdministratorResponse>(AuthErrors.Forbidden);
        }

        // Step: Normalize username for display and unique comparison
        string displayUsername = UsernameNormalization.GetDisplayUsername(request.Username);
        string normalizedUsername = UsernameNormalization.GetNormalizedUsername(displayUsername);

        // State: Uniqueness check - Verify username is not taken by any existing Host or Administrator
        bool usernameExists = await _dbContext.Users
            .AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);

        if (usernameExists)
        {
            return Result.Failure<AdministratorResponse>(AccountErrors.Conflict);
        }

        // Step: Compute cryptographic password hash using configured argon2id algorithm
        string passwordHash;
        try
        {
            passwordHash = await _passwordHasher.HashPasswordAsync(request.Password, cancellationToken);
        }
        catch (PasswordHashingRateLimitedException)
        {
            return Result.Failure<AdministratorResponse>(AuthErrors.RateLimited);
        }

        // Step: Instantiate new SystemAdmin user entity with initial security revision
        User user = new User
        {
            Id = Guid.NewGuid(),
            DisplayUsername = displayUsername,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash,
            Role = UserRole.SystemAdmin,
            Status = UserStatus.Active,
            TokenSecurityVersion = 1,
            Revision = 1,
            TerminationPending = false
        };

        _dbContext.Users.Add(user);

        // Step: Persist administrator to database and catch race condition on unique username constraint
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (string.Equals(ex.ConstraintName, NormalizedUsernameConstraintName, StringComparison.Ordinal))
        {
            return Result.Failure<AdministratorResponse>(AccountErrors.Conflict);
        }

        return Result.Success(new AdministratorResponse(
            user.Id,
            user.DisplayUsername,
            user.Role,
            user.Status,
            user.CreatedAt,
            null,
            user.Revision,
            user.TerminationPending));
    }
}
