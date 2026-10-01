namespace Kahoot.Application.IntegrationTests.Features.Auth;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.Features.Auth.Logout;
using Kahoot.Application.Features.Auth.LogoutAll;
using Kahoot.Application.Features.Auth.Refresh;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Auth")]
[Trait("Phase", "04")]
public sealed class LogoutTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public LogoutTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Logout_NullUnknownOrAlreadyRevokedToken_IsIdempotent()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        // 1. Null token
        LogoutCommand nullCommand = new(null);
        Result nullResult = await harness.SendAsync(nullCommand, TestCaller.Anonymous);
        Assert.True(nullResult.IsSuccess);

        // 2. Unknown token
        LogoutCommand unknownCommand = new("unknown_token_value_here");
        Result unknownResult = await harness.SendAsync(unknownCommand, TestCaller.Anonymous);
        Assert.True(unknownResult.IsSuccess);
    }

    [Fact]
    public async Task Logout_OldRotatedToken_RevokesItsReplacementFamily()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LogoutHost", "Password123!@#");

        // Login 1 (Family A)
        LoginCommand loginA = new("LogoutHost", "Password123!@#", "127.0.0.1");
        Result<LoginResult> loginAResult = await harness.SendAsync(loginA, TestCaller.Anonymous);
        Assert.True(loginAResult.IsSuccess);
        string tokenA1 = loginAResult.Value.RawRefreshToken;

        // Login 2 (Family B)
        LoginCommand loginB = new("LogoutHost", "Password123!@#", "127.0.0.2");
        Result<LoginResult> loginBResult = await harness.SendAsync(loginB, TestCaller.Anonymous);
        Assert.True(loginBResult.IsSuccess);
        string tokenB1 = loginBResult.Value.RawRefreshToken;

        // Rotate Family A
        RefreshCommand refreshA = new(tokenA1);
        Result<RefreshResult> refreshAResult = await harness.SendAsync(refreshA, TestCaller.Anonymous);
        Assert.True(refreshAResult.IsSuccess);

        // Logout using old rotated token tokenA1
        LogoutCommand logoutCommand = new(tokenA1);
        Result logoutResult = await harness.SendAsync(logoutCommand, TestCaller.Anonymous);
        Assert.True(logoutResult.IsSuccess);

        List<RefreshToken> allTokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).ToListAsync(ct));

        Assert.Equal(3, allTokens.Count);

        byte[] tokenA1Hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tokenA1));
        RefreshToken? tokenA1Record = allTokens.FirstOrDefault(t => t.TokenHash.SequenceEqual(tokenA1Hash));
        Assert.NotNull(tokenA1Record);

        Guid familyAId = tokenA1Record.TokenFamilyId;
        List<RefreshToken> familyATokens = allTokens.Where(t => t.TokenFamilyId == familyAId).ToList();
        List<RefreshToken> familyBTokens = allTokens.Where(t => t.TokenFamilyId != familyAId).ToList();

        Assert.Equal(2, familyATokens.Count);
        Assert.All(familyATokens, t => Assert.NotNull(t.RevokedAt));

        Assert.Single(familyBTokens);
        Assert.All(familyBTokens, t => Assert.Null(t.RevokedAt));
    }

    [Fact]
    public async Task LogoutAll_ActiveUser_RevokesAllFamiliesAndIncrementsSecurityVersion()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LogoutAllHost", "Password123!@#");

        LoginCommand login1 = new("LogoutAllHost", "Password123!@#", "127.0.0.1");
        Result<LoginResult> login1Result = await harness.SendAsync(login1, TestCaller.Anonymous);
        Assert.True(login1Result.IsSuccess);

        LoginCommand login2 = new("LogoutAllHost", "Password123!@#", "127.0.0.2");
        Result<LoginResult> login2Result = await harness.SendAsync(login2, TestCaller.Anonymous);
        Assert.True(login2Result.IsSuccess);

        LogoutAllCommand logoutAllCommand = new();
        Result logoutAllResult = await harness.SendAsync(logoutAllCommand, TestCaller.Host(host.UserId));
        Assert.True(logoutAllResult.IsSuccess);

        List<RefreshToken> allTokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).ToListAsync(ct));

        Assert.Equal(2, allTokens.Count);
        Assert.All(allTokens, t => Assert.NotNull(t.RevokedAt));

        User? user = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == host.UserId, ct));

        Assert.NotNull(user);
        Assert.Equal(2, user.TokenSecurityVersion);
        Assert.Equal(1L, user.Revision);

        RecordingSocketEvictionService evictionService = harness.Services.GetRequiredService<RecordingSocketEvictionService>();
        Assert.Single(evictionService.Records);
        Assert.Equal(host.UserId, evictionService.Records[0].HostAccountId);
    }

    [Fact]
    public async Task LogoutAll_Anonymous_ReturnsUnauthorized()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        LogoutAllCommand command = new();
        Result result = await harness.SendAsync(command, TestCaller.Anonymous);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, result.Error.Code);
    }
}
