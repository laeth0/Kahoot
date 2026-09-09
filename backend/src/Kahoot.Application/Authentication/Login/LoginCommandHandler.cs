using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Domain.Common;
using Kahoot.Domain.Hosts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Authentication.Login;

internal sealed class LoginCommandHandler(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ISecureTokenGenerator secureTokenGenerator,
    ITokenHasher tokenHasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, AuthenticationResponse>
{
    private const string DummyPasswordHash = "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW";

    public async Task<Result<AuthenticationResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        string username = command.Username.Trim().ToLowerInvariant();

        Host? host = await dbContext.Hosts
            .FirstOrDefaultAsync(candidate => candidate.Username == username, cancellationToken);

        bool passwordValid = passwordHasher.Verify(command.Password, host?.PasswordHash ?? DummyPasswordHash);

        if (host is null || !passwordValid)
        {
            return Result.Failure<AuthenticationResponse>(AuthenticationErrors.InvalidCredentials);
        }

        (RefreshToken refreshToken, AuthenticationResponse response) = AuthTokenFactory.Issue(
            host,
            jwtTokenService,
            secureTokenGenerator,
            tokenHasher,
            jwtOptions.Value.RefreshTokenDays,
            timeProvider.GetUtcNow());

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }
}
