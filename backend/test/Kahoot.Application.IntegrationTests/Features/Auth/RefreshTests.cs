namespace Kahoot.Application.IntegrationTests.Features.Auth;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.Features.Auth.Refresh;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Auth")]
[Trait("Phase", "04")]
public sealed class RefreshTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public RefreshTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesWithinSameFamily()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RefreshHost", "Password123!@#");

        LoginCommand loginCommand = new("RefreshHost", "Password123!@#", "127.0.0.1");
        Result<LoginResult> loginResult = await harness.SendAsync(loginCommand, TestCaller.Anonymous);
        Assert.True(loginResult.IsSuccess);

        string originalRawToken = loginResult.Value.RawRefreshToken;
        byte[] originalHash = SHA256.HashData(Encoding.UTF8.GetBytes(originalRawToken));

        RefreshCommand refreshCommand = new(originalRawToken);
        Result<RefreshResult> refreshResult = await harness.SendAsync(refreshCommand, TestCaller.Anonymous);

        Assert.True(refreshResult.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.Value.Response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.Value.RawRefreshToken));
        Assert.NotEqual(originalRawToken, refreshResult.Value.RawRefreshToken);

        byte[] replacementHash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshResult.Value.RawRefreshToken));

        List<RefreshToken> tokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).OrderBy(t => t.CreatedAt).ToListAsync(ct));

        Assert.Equal(2, tokens.Count);

        RefreshToken oldToken = tokens[0];
        RefreshToken newToken = tokens[1];

        Assert.Equal(originalHash, oldToken.TokenHash);
        Assert.NotNull(oldToken.RotatedAt);
        Assert.Null(oldToken.RevokedAt);

        Assert.Equal(replacementHash, newToken.TokenHash);
        Assert.Equal(oldToken.TokenFamilyId, newToken.TokenFamilyId);
        Assert.Equal(oldToken.FamilyCreatedAt, newToken.FamilyCreatedAt);
        Assert.Null(newToken.RotatedAt);
        Assert.Null(newToken.RevokedAt);

        User? user = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == host.UserId, ct));

        Assert.NotNull(user);
        Assert.Equal(1, user.TokenSecurityVersion);
    }

    [Fact]
    public async Task Refresh_RotatedTokenBeforeTenSeconds_ReturnsRefreshRace()
    {
        DateTimeOffset initialTime = DateTimeOffset.UtcNow;
        AdjustableTimeProvider timeProvider = new(initialTime);

        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            ServiceDescriptor? existing = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
            if (existing is not null)
            {
                services.Remove(existing);
            }
            services.AddSingleton<TimeProvider>(timeProvider);
        });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RaceHost", "Password123!@#");

        LoginCommand loginCommand = new("RaceHost", "Password123!@#", "127.0.0.1");
        Result<LoginResult> loginResult = await harness.SendAsync(loginCommand, TestCaller.Anonymous);
        Assert.True(loginResult.IsSuccess);

        string originalToken = loginResult.Value.RawRefreshToken;

        RefreshCommand firstRefresh = new(originalToken);
        Result<RefreshResult> firstRefreshResult = await harness.SendAsync(firstRefresh, TestCaller.Anonymous);
        Assert.True(firstRefreshResult.IsSuccess);

        // Advance 9 seconds (within 10-second grace period)
        timeProvider.Advance(TimeSpan.FromSeconds(9));

        RefreshCommand raceRefresh = new(originalToken);
        Result<RefreshResult> raceRefreshResult = await harness.SendAsync(raceRefresh, TestCaller.Anonymous);

        Assert.False(raceRefreshResult.IsSuccess);
        Assert.Equal(AuthErrors.RefreshRace.Code, raceRefreshResult.Error.Code);

        // Verify tokens count and revocation
        List<RefreshToken> tokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).ToListAsync(ct));

        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.Null(t.RevokedAt));

        RecordingSocketEvictionService evictionService = harness.Services.GetRequiredService<RecordingSocketEvictionService>();
        Assert.Empty(evictionService.Records);
    }

    [Fact]
    public async Task Refresh_RotatedTokenAtTenSeconds_RevokesOnlyItsFamily()
    {
        DateTimeOffset initialTime = DateTimeOffset.UtcNow;
        AdjustableTimeProvider timeProvider = new(initialTime);

        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            ServiceDescriptor? existing = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
            if (existing is not null)
            {
                services.Remove(existing);
            }
            services.AddSingleton<TimeProvider>(timeProvider);
        });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReuseHost", "Password123!@#");

        // Login 1 (Family A)
        LoginCommand loginA = new("ReuseHost", "Password123!@#", "127.0.0.1");
        Result<LoginResult> loginAResult = await harness.SendAsync(loginA, TestCaller.Anonymous);
        Assert.True(loginAResult.IsSuccess);
        string tokenA1 = loginAResult.Value.RawRefreshToken;

        // Login 2 (Family B)
        LoginCommand loginB = new("ReuseHost", "Password123!@#", "127.0.0.2");
        Result<LoginResult> loginBResult = await harness.SendAsync(loginB, TestCaller.Anonymous);
        Assert.True(loginBResult.IsSuccess);

        // Rotate Family A
        RefreshCommand refreshA = new(tokenA1);
        Result<RefreshResult> refreshAResult = await harness.SendAsync(refreshA, TestCaller.Anonymous);
        Assert.True(refreshAResult.IsSuccess);

        // Advance 10 seconds exactly (grace expired)
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Replay tokenA1
        RefreshCommand replayCommand = new(tokenA1);
        Result<RefreshResult> replayResult = await harness.SendAsync(replayCommand, TestCaller.Anonymous);

        Assert.False(replayResult.IsSuccess);
        Assert.Equal(AuthErrors.RefreshTokenReuse.Code, replayResult.Error.Code);

        // Family A tokens must be revoked
        byte[] hashA1 = SHA256.HashData(Encoding.UTF8.GetBytes(tokenA1));
        RefreshToken? familyAToken = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hashA1, ct));

        Assert.NotNull(familyAToken);
        Guid familyAId = familyAToken.TokenFamilyId;

        List<RefreshToken> allTokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).ToListAsync(ct));

        List<RefreshToken> familyATokens = allTokens.Where(t => t.TokenFamilyId == familyAId).ToList();
        List<RefreshToken> familyBTokens = allTokens.Where(t => t.TokenFamilyId != familyAId).ToList();

        Assert.Equal(2, familyATokens.Count);
        Assert.All(familyATokens, t => Assert.NotNull(t.RevokedAt));

        Assert.Single(familyBTokens);
        Assert.All(familyBTokens, t => Assert.Null(t.RevokedAt));

        User? user = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == host.UserId, ct));

        Assert.NotNull(user);
        Assert.Equal(2, user.TokenSecurityVersion);

        RecordingSocketEvictionService evictionService = harness.Services.GetRequiredService<RecordingSocketEvictionService>();
        Assert.Single(evictionService.Records);
        Assert.Equal(host.UserId, evictionService.Records[0].HostAccountId);
    }

    [Fact]
    public async Task Refresh_ExpiredOrFamilyLifetimeReached_IsInvalid()
    {
        DateTimeOffset initialTime = DateTimeOffset.UtcNow;
        AdjustableTimeProvider timeProvider = new(initialTime);

        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            ServiceDescriptor? existing = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
            if (existing is not null)
            {
                services.Remove(existing);
            }
            services.AddSingleton<TimeProvider>(timeProvider);
        });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ExpiryHost", "Password123!@#");

        LoginCommand login = new("ExpiryHost", "Password123!@#", "127.0.0.1");
        Result<LoginResult> loginResult = await harness.SendAsync(login, TestCaller.Anonymous);
        Assert.True(loginResult.IsSuccess);
        string rawToken = loginResult.Value.RawRefreshToken;

        // Advance 15 days (LifetimeDays is 14)
        timeProvider.Advance(TimeSpan.FromDays(15));

        RefreshCommand refreshCommand = new(rawToken);
        Result<RefreshResult> result = await harness.SendAsync(refreshCommand, TestCaller.Anonymous);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.InvalidRefreshToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task Refresh_UnknownRevokedOrSuspendedUser_IsInvalid()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord activeHost = await FeatureData.CreateUserAsync(harness, "ActiveUser", "Password123!@#");
        TestUserRecord suspendedHost = await FeatureData.CreateUserAsync(harness, "SuspendedUser", "Password123!@#", status: UserStatus.Suspended);

        // Unknown token
        RefreshCommand unknownCommand = new("non_existent_token_12345");
        Result<RefreshResult> unknownResult = await harness.SendAsync(unknownCommand, TestCaller.Anonymous);
        Assert.False(unknownResult.IsSuccess);
        Assert.Equal(AuthErrors.InvalidRefreshToken.Code, unknownResult.Error.Code);

        // Suspended user token
        LoginCommand loginSuspended = new("SuspendedUser", "Password123!@#", "127.0.0.1");
        // Suspended user login fails, but let's insert a token manually for suspended host
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string suspendedRawToken = "suspended_user_token_sample_12345";
        byte[] suspendedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suspendedRawToken));

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Anonymous))
        {
            RefreshToken suspendedToken = new()
            {
                Id = Guid.NewGuid(),
                UserId = suspendedHost.UserId,
                TokenFamilyId = Guid.NewGuid(),
                FamilyCreatedAt = now,
                TokenHash = suspendedHash,
                CreatedAt = now,
                ExpiresAt = now.AddDays(14)
            };
            scope.DbContext.RefreshTokens.Add(suspendedToken);
            await scope.DbContext.SaveChangesAsync();
        }

        RefreshCommand suspendedRefresh = new(suspendedRawToken);
        Result<RefreshResult> suspendedResult = await harness.SendAsync(suspendedRefresh, TestCaller.Anonymous);
        Assert.False(suspendedResult.IsSuccess);
        Assert.Equal(AuthErrors.InvalidRefreshToken.Code, suspendedResult.Error.Code);
    }
}
