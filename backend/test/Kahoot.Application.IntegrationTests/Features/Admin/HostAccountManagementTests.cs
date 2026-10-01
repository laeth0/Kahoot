namespace Kahoot.Application.IntegrationTests.Features.Admin;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin;
using Kahoot.Application.Features.Admin.Users.ReactivateUser;
using Kahoot.Application.Features.Admin.Users.SuspendUser;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Admin")]
[Trait("Phase", "12")]
public sealed class HostAccountManagementTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public HostAccountManagementTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SuspendUser_ValidHostWithoutUnfinishedGames_SuspendsWithoutFinalizerHint()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "SysAdmin1", role: UserRole.SystemAdmin);
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "HostToSuspend1", role: UserRole.Host);

        // Seed a refresh token for the host
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Admin(admin.UserId)))
        {
            scope.DbContext.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = host.UserId,
                TokenFamilyId = Guid.NewGuid(),
                TokenHash = new byte[32],
                CreatedAt = now,
                FamilyCreatedAt = now,
                ExpiresAt = now.AddDays(14),
                RevokedAt = null
            });
            await scope.DbContext.SaveChangesAsync();
        }

        RecordingSuspensionFinalizerChannel finalizerChannel = harness.Services.GetRequiredService<RecordingSuspensionFinalizerChannel>();
        int initialHintCount = finalizerChannel.Suspensions.Count;

        SuspendUserCommand suspendCommand = new(host.UserId, Revision: 1);
        Result<SuspendUserResult> result = await harness.SendAsync(suspendCommand, TestCaller.Admin(admin.UserId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.TerminationPending);
        Assert.Equal(initialHintCount, finalizerChannel.Suspensions.Count);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            User user = await dbContext.Users.FirstAsync(u => u.Id == host.UserId);
            Assert.Equal(UserStatus.Suspended, user.Status);
            Assert.Equal(2, user.Revision);
            Assert.Equal(2, user.TokenSecurityVersion);
            Assert.False(user.TerminationPending);
            Assert.Equal(admin.UserId, user.UpdatedBy);

            List<RefreshToken> tokens = await dbContext.RefreshTokens
                .Where(t => t.UserId == host.UserId)
                .ToListAsync();
            Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
        });
    }

    [Fact]
    public async Task SuspendUser_WithUnfinishedGame_SetsTerminationPendingAndDispatchesHint()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "SysAdmin2", role: UserRole.SystemAdmin);
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "HostWithLiveGame", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Active Quiz");

        // Host creates game (status Lobby)
        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        RecordingSuspensionFinalizerChannel finalizerChannel = harness.Services.GetRequiredService<RecordingSuspensionFinalizerChannel>();

        // Admin suspends host (revision is 1)
        SuspendUserCommand suspendCommand = new(host.UserId, Revision: 1);
        Result<SuspendUserResult> result = await harness.SendAsync(suspendCommand, TestCaller.Admin(admin.UserId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.TerminationPending);
        Assert.Contains(finalizerChannel.Suspensions, id => id == host.UserId);

        // Verify unfinished game is still present and not deleted
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.Lobby, game.Status);

            User user = await dbContext.Users.FirstAsync(u => u.Id == host.UserId);
            Assert.Equal(UserStatus.Suspended, user.Status);
            Assert.True(user.TerminationPending);
        });

        // Reactivation while TerminationPending is blocked
        ReactivateUserCommand reactivateCommand = new(host.UserId, Revision: 2);
        Result reactivateResult = await harness.SendAsync(reactivateCommand, TestCaller.Admin(admin.UserId));
        Assert.True(reactivateResult.IsFailure);
        Assert.Equal(AccountErrors.TerminationPending.Code, reactivateResult.Error.Code);
    }

    [Fact]
    public async Task SuspendAndReactivate_IdempotentRetriesAndAuditIntegrity()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "SysAdmin3", role: UserRole.SystemAdmin);
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "HostLifecycle", role: UserRole.Host);

        // 1. Suspend with current revision 1
        SuspendUserCommand suspend1 = new(host.UserId, Revision: 1);
        Result<SuspendUserResult> suspendResult1 = await harness.SendAsync(suspend1, TestCaller.Admin(admin.UserId));
        Assert.True(suspendResult1.IsSuccess);

        // 2. Retry suspend with original revision 1 (idempotent replay branch)
        Result<SuspendUserResult> retrySuspend = await harness.SendAsync(suspend1, TestCaller.Admin(admin.UserId));
        Assert.True(retrySuspend.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            User user = await dbContext.Users.FirstAsync(u => u.Id == host.UserId);
            Assert.Equal(2, user.Revision); // Did not increment to 3
        });

        // 3. Reactivate with current revision 2
        ReactivateUserCommand reactivate = new(host.UserId, Revision: 2);
        Result reactivateResult = await harness.SendAsync(reactivate, TestCaller.Admin(admin.UserId));
        Assert.True(reactivateResult.IsSuccess);

        // 4. Retry reactivate with original revision 2 (idempotent replay branch)
        Result retryReactivate = await harness.SendAsync(reactivate, TestCaller.Admin(admin.UserId));
        Assert.True(retryReactivate.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            User user = await dbContext.Users.FirstAsync(u => u.Id == host.UserId);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.Equal(3, user.Revision);
            Assert.Equal(admin.UserId, user.UpdatedBy);
        });
    }

    [Fact]
    public async Task SuspendUser_ErrorBranches_EnforceSecurityAndConcurrency()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "SysAdmin4", role: UserRole.SystemAdmin);
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "HostErrors", role: UserRole.Host);

        // 1. Non-admin caller -> Forbidden
        SuspendUserCommand cmd = new(host.UserId, Revision: 1);
        Result<SuspendUserResult> forbiddenResult = await harness.SendAsync(cmd, TestCaller.Host(host.UserId));
        Assert.True(forbiddenResult.IsFailure);
        Assert.Equal(AuthErrors.Forbidden.Code, forbiddenResult.Error.Code);

        // 2. Stale revision -> ConcurrentModification
        SuspendUserCommand staleCmd = new(host.UserId, Revision: 99);
        Result<SuspendUserResult> staleResult = await harness.SendAsync(staleCmd, TestCaller.Admin(admin.UserId));
        Assert.True(staleResult.IsFailure);
        Assert.Equal(AccountErrors.ConcurrentModification.Code, staleResult.Error.Code);

        // 3. Target is SystemAdmin instead of Host -> NotFound
        SuspendUserCommand adminTargetCmd = new(admin.UserId, Revision: 1);
        Result<SuspendUserResult> targetResult = await harness.SendAsync(adminTargetCmd, TestCaller.Admin(admin.UserId));
        Assert.True(targetResult.IsFailure);
        Assert.Equal(AccountErrors.NotFound.Code, targetResult.Error.Code);
    }
}
