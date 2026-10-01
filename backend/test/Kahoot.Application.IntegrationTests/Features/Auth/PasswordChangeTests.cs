namespace Kahoot.Application.IntegrationTests.Features.Auth;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.ChangePassword;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Auth")]
[Trait("Phase", "04")]
public sealed class PasswordChangeTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public PasswordChangeTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ChangePassword_ValidCurrentPassword_CommitsHashAndRevocation()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "ChangePassHost",
            password: "OldPassword123!@#");

        LoginCommand login = new("ChangePassHost", "OldPassword123!@#", "127.0.0.1");
        Result<LoginResult> loginResult = await harness.SendAsync(login, TestCaller.Anonymous);
        Assert.True(loginResult.IsSuccess);

        ChangePasswordCommand command = new("OldPassword123!@#", "NewPassword123!@#");
        Result result = await harness.SendAsync(command, TestCaller.Host(host.UserId));
        Assert.True(result.IsSuccess);

        User? user = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == host.UserId, ct));

        Assert.NotNull(user);
        Assert.Equal(2, user.TokenSecurityVersion);
        Assert.Equal(2L, user.Revision);

        IPasswordHasher passwordHasher = harness.Services.GetRequiredService<IPasswordHasher>();
        bool newPasswordMatches = await passwordHasher.VerifyPasswordAsync("NewPassword123!@#", user.PasswordHash);
        bool oldPasswordMatches = await passwordHasher.VerifyPasswordAsync("OldPassword123!@#", user.PasswordHash);

        Assert.True(newPasswordMatches);
        Assert.False(oldPasswordMatches);

        List<RefreshToken> tokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).ToListAsync(ct));

        Assert.Single(tokens);
        Assert.NotNull(tokens[0].RevokedAt);

        RecordingSocketEvictionService evictionService = harness.Services.GetRequiredService<RecordingSocketEvictionService>();
        Assert.Single(evictionService.Records);
        Assert.Equal(host.UserId, evictionService.Records[0].HostAccountId);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_DoesNotMutate()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "WrongPassHost",
            password: "RealPassword123!@#");

        LoginCommand login = new("WrongPassHost", "RealPassword123!@#", "127.0.0.1");
        Result<LoginResult> loginResult = await harness.SendAsync(login, TestCaller.Anonymous);
        Assert.True(loginResult.IsSuccess);

        ChangePasswordCommand command = new("IncorrectPassword123!@#", "NewPassword123!@#");
        Result result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error.Code);

        User? user = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == host.UserId, ct));

        Assert.NotNull(user);
        Assert.Equal(1, user.TokenSecurityVersion);
        Assert.Equal(1L, user.Revision);

        IPasswordHasher passwordHasher = harness.Services.GetRequiredService<IPasswordHasher>();
        bool originalPasswordStillMatches = await passwordHasher.VerifyPasswordAsync("RealPassword123!@#", user.PasswordHash);
        Assert.True(originalPasswordStillMatches);

        List<RefreshToken> tokens = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == host.UserId).ToListAsync(ct));

        Assert.Single(tokens);
        Assert.Null(tokens[0].RevokedAt);

        RecordingSocketEvictionService evictionService = harness.Services.GetRequiredService<RecordingSocketEvictionService>();
        Assert.Empty(evictionService.Records);
    }

    [Fact]
    public async Task ChangePassword_Anonymous_ReturnsUnauthorized()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        ChangePasswordCommand command = new("SomePassword123!@#", "NewPassword123!@#");
        Result result = await harness.SendAsync(command, TestCaller.Anonymous);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, result.Error.Code);
    }
}
