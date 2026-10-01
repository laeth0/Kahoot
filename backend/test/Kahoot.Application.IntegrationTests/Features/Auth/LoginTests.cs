namespace Kahoot.Application.IntegrationTests.Features.Auth;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Auth")]
[Trait("Phase", "03")]
public sealed class LoginTests
{
    private static readonly byte[] StaticSigningKey = new byte[32]
    {
        0x4b, 0x61, 0x68, 0x6f, 0x6f, 0x74, 0x54, 0x65,
        0x73, 0x74, 0x53, 0x69, 0x67, 0x6e, 0x69, 0x6e,
        0x67, 0x4b, 0x65, 0x79, 0x32, 0x30, 0x32, 0x36,
        0x53, 0x65, 0x63, 0x75, 0x72, 0x65, 0x21, 0x21
    };

    private readonly ApplicationDependencyFixture _fixture;

    public LoginTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_ValidCredentials_PersistsHashedRefreshFamily()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "LoginHost",
            password: "LoginPassword123!@#");

        LoginCommand command = new("LoginHost", "LoginPassword123!@#", "192.168.1.100");
        Result<LoginResult> result = await harness.SendAsync(command, TestCaller.Anonymous);

        Assert.True(result.IsSuccess);
        Assert.Equal(host.UserId, result.Value.Response.AccountId);
        Assert.Equal("LoginHost", result.Value.Response.Username);
        Assert.Equal("Host", result.Value.Response.AccountKind);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawRefreshToken));

        JsonWebTokenHandler tokenHandler = new();
        TokenValidationParameters validationParameters = new()
        {
            ValidIssuer = "Kahoot.IntegrationTests",
            ValidAudience = "Kahoot.Client",
            IssuerSigningKey = new SymmetricSecurityKey(StaticSigningKey),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true
        };

        TokenValidationResult validationResult = await tokenHandler.ValidateTokenAsync(
            result.Value.Response.AccessToken,
            validationParameters);

        Assert.True(validationResult.IsValid);
        Assert.Equal(host.UserId.ToString(), validationResult.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal("Host", validationResult.ClaimsIdentity.FindFirst("role")?.Value);
        Assert.Equal("1", validationResult.ClaimsIdentity.FindFirst("token_security_version")?.Value);

        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(result.Value.RawRefreshToken));

        RefreshToken? persistedToken = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == host.UserId, ct));

        Assert.NotNull(persistedToken);
        Assert.Equal(expectedHash, persistedToken.TokenHash);
        Assert.Null(persistedToken.RotatedAt);
        Assert.Null(persistedToken.RevokedAt);
        Assert.True(persistedToken.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_Twice_CreatesIndependentFamilies()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "DualLoginHost",
            password: "DualPassword123!@#");

        LoginCommand firstCommand = new("DualLoginHost", "DualPassword123!@#", "192.168.1.101");
        Result<LoginResult> firstResult = await harness.SendAsync(firstCommand, TestCaller.Anonymous);
        Assert.True(firstResult.IsSuccess);

        LoginCommand secondCommand = new("DualLoginHost", "DualPassword123!@#", "192.168.1.102");
        Result<LoginResult> secondResult = await harness.SendAsync(secondCommand, TestCaller.Anonymous);
        Assert.True(secondResult.IsSuccess);

        Assert.NotEqual(firstResult.Value.RawRefreshToken, secondResult.Value.RawRefreshToken);

        List<RefreshToken> userTokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).ToListAsync(ct));

        Assert.Equal(2, userTokens.Count);
        Assert.NotEqual(userTokens[0].TokenFamilyId, userTokens[1].TokenFamilyId);
        Assert.NotEqual(userTokens[0].TokenHash, userTokens[1].TokenHash);
        Assert.Null(userTokens[0].RevokedAt);
        Assert.Null(userTokens[1].RevokedAt);
    }

    [Fact]
    public async Task Login_UnknownWrongPasswordOrSuspended_ConcealsAccountState()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord activeHost = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "ActiveHost",
            password: "CorrectPassword123!@#",
            status: UserStatus.Active);

        TestUserRecord suspendedHost = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "SuspendedHost",
            password: "CorrectPassword123!@#",
            status: UserStatus.Suspended);

        // Case A: Unknown user
        LoginCommand unknownCommand = new("NonExistentUser", "AnyPassword123!@#", "10.0.0.1");
        Result<LoginResult> unknownResult = await harness.SendAsync(unknownCommand, TestCaller.Anonymous);
        Assert.False(unknownResult.IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, unknownResult.Error.Code);

        // Case B: Wrong password for existing user
        LoginCommand wrongPassCommand = new("ActiveHost", "WrongPassword123!@#", "10.0.0.2");
        Result<LoginResult> wrongPassResult = await harness.SendAsync(wrongPassCommand, TestCaller.Anonymous);
        Assert.False(wrongPassResult.IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, wrongPassResult.Error.Code);

        // Case C: Suspended user with correct password
        LoginCommand suspendedCommand = new("SuspendedHost", "CorrectPassword123!@#", "10.0.0.3");
        Result<LoginResult> suspendedResult = await harness.SendAsync(suspendedCommand, TestCaller.Anonymous);
        Assert.False(suspendedResult.IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, suspendedResult.Error.Code);

        int totalTokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.CountAsync(ct));

        Assert.Equal(0, totalTokens);
    }

    [Fact]
    public async Task Login_AtIpCap_RejectsBeforeCreatingTokens()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "IpCapHost",
            password: "IpCapPassword123!@#");

        string testIp = "198.51.100.42";
        ILoginRateLimiter rateLimiter = harness.Services.GetRequiredService<ILoginRateLimiter>();

        // Consume 30 admissions directly without hitting database or Argon2
        for (int i = 0; i < 30; i++)
        {
            bool isLimited = rateLimiter.IsIpRateLimited(testIp);
            Assert.False(isLimited);
        }

        // Next login attempt with this IP should be rate limited
        LoginCommand command = new("IpCapHost", "IpCapPassword123!@#", testIp);
        Result<LoginResult> result = await harness.SendAsync(command, TestCaller.Anonymous);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.RateLimited.Code, result.Error.Code);

        int tokenCount = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.CountAsync(t => t.UserId == host.UserId, ct));

        Assert.Equal(0, tokenCount);
    }
}
