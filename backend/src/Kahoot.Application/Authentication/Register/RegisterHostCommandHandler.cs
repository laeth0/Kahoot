using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Domain.Common;
using Kahoot.Domain.Hosts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Authentication.Register;

internal sealed class RegisterHostCommandHandler(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ISecureTokenGenerator secureTokenGenerator,
    ITokenHasher tokenHasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : ICommandHandler<RegisterHostCommand, AuthenticationResponse>
{
    public async Task<Result<AuthenticationResponse>> Handle(
        RegisterHostCommand command,
        CancellationToken cancellationToken)
    {
        string username = command.Username.Trim().ToLowerInvariant();

        bool usernameTaken = await dbContext.Hosts
            .AnyAsync(host => host.Username == username, cancellationToken);
        if (usernameTaken)
        {
            return Result.Failure<AuthenticationResponse>(AuthenticationErrors.UsernameTaken);
        }

        Host host = new()
        {
            Username = username,
            PasswordHash = passwordHasher.Hash(command.Password)
        };

        (RefreshToken refreshToken, AuthenticationResponse response) = AuthTokenFactory.Issue(
            host,
            jwtTokenService,
            secureTokenGenerator,
            tokenHasher,
            jwtOptions.Value.RefreshTokenDays,
            timeProvider.GetUtcNow());

        dbContext.Hosts.Add(host);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }
}
