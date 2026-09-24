using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler : ICommandHandler<RegisterCommand, RegisterResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public RegisterCommandHandler(
        IAppDbContext dbContext,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var displayUsername = request.Username.Trim();
        var normalizedUsername = displayUsername.Normalize(NormalizationForm.FormKC).ToUpperInvariant();

        var usernameExists = await _dbContext.Users
            .AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);

        if (usernameExists)
        {
            return Result.Failure<RegisterResponse>(AuthErrors.UsernameUnavailable);
        }

        var utcNow = _timeProvider.GetUtcNow();
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayUsername = displayUsername,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash,
            Role = UserRole.Host,
            Status = UserStatus.Active,
            TokenSecurityVersion = 1,
            Revision = 1,
            TerminationPending = false,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Result.Failure<RegisterResponse>(AuthErrors.UsernameUnavailable);
        }

        return Result.Success(new RegisterResponse(user.Id, user.DisplayUsername));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var sqlStateProperty = inner.GetType().GetProperty("SqlState");
        if (sqlStateProperty?.GetValue(inner) is string sqlState && sqlState == "23505")
        {
            return true;
        }

        return inner.Message.Contains("23505") || inner.Message.Contains("ux_users_normalized_username");
    }
}
