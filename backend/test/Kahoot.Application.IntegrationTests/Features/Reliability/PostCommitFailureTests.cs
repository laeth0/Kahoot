namespace Kahoot.Application.IntegrationTests.Features.Reliability;

using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth.LogoutAll;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Reliability")]
[Trait("Phase", "16")]
public sealed class PostCommitFailureTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public PostCommitFailureTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GameBroadcast_RequestCancelledAfterCommit_UsesIndependentToken()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "PostCommitHost1", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "PostCommit Quiz 1");

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
        await harness.SendAsync(new JoinGameCommand(createRes.Pin, "Player1", Guid.NewGuid(), "10.0.0.1"), TestCaller.Anonymous);

        CancellationTokenSource cts = new();
        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();

        bool callbackExecuted = false;
        CancellationToken receivedNotificationToken = CancellationToken.None;

        notificationService.Callback = (GameNotificationRecord record) =>
        {
            callbackExecuted = true;
            receivedNotificationToken = record.CancellationToken;
            // Cancel the caller's cancellation token now that commit is complete
            cts.Cancel();
            return Task.CompletedTask;
        };

        StartGameCommand startCmd = new(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1);

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Result<StartGameResponse> startResult = await scope.Sender.Send(startCmd, cts.Token);
            Assert.True(startResult.IsSuccess);
        }

        Assert.True(callbackExecuted);
        Assert.Equal(CancellationToken.None, receivedNotificationToken);

        // Verify committed state
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(GameStatus.QuestionActive, game.Status);
            Assert.Equal(2, game.StateVersion);
        });
    }

    [Fact]
    public async Task GameBroadcast_ThrowsAfterCommit_StateAndReplayRemainDurable()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "PostCommitHost2", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "PostCommit Quiz 2");

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
        await harness.SendAsync(new JoinGameCommand(createRes.Pin, "Player1", Guid.NewGuid(), "10.0.0.1"), TestCaller.Anonymous);

        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();

        notificationService.Callback = (GameNotificationRecord record) =>
        {
            throw new InvalidOperationException("Simulated Redis broadcast network outage");
        };

        Guid commandId = Guid.NewGuid();
        StartGameCommand startCmd = new(createRes.GameId, commandId, ExpectedStateVersion: 1);

        // Handler commits, then broadcast throws InvalidOperationException which propagates out
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await harness.SendAsync(startCmd, TestCaller.Host(host.UserId));
        });

        // Verify state was already durably committed before the broadcast failure
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(GameStatus.QuestionActive, game.Status);
            Assert.Equal(2, game.StateVersion);

            bool idempotencySaved = await dbContext.GameCommandIdempotencies
                .AnyAsync(i => i.GameId == createRes.GameId && i.CommandId == commandId);
            Assert.True(idempotencySaved);
        });

        // Reset the callback to allow notifications
        notificationService.Callback = null;

        // Command replay: Retrying with the same command ID safely returns the cached response
        Result<StartGameResponse> replayResult = await harness.SendAsync(startCmd, TestCaller.Host(host.UserId));
        Assert.True(replayResult.IsSuccess);
        Assert.Equal(createRes.GameId, replayResult.Value.GameId);
        Assert.Equal(2, replayResult.Value.StateVersion);
    }

    [Fact]
    public async Task AuthEviction_CallerCancellationAfterCommit_DoesNotUndoRevocation()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord user = await FeatureData.CreateUserAsync(harness, "EvictUser", role: UserRole.Host);

        // Seed an active refresh token for this user
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            dbContext.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                TokenHash = SHA256.HashData(Guid.NewGuid().ToByteArray()),
                TokenFamilyId = Guid.NewGuid(),
                FamilyCreatedAt = now,
                ExpiresAt = now.AddDays(7),
                CreatedAt = now,
                RevokedAt = null
            });
            await dbContext.SaveChangesAsync();
        });

        int initialSecurityVersion = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.Users
                .Where(u => u.Id == user.UserId)
                .Select(u => u.TokenSecurityVersion)
                .FirstAsync();
        });

        CancellationTokenSource cts = new();
        RecordingSocketEvictionService evictionService = harness.Services.GetRequiredService<RecordingSocketEvictionService>();

        evictionService.Callback = (SocketEvictionRecord record) =>
        {
            // Simulate cancellation triggering in the eviction step using the caller's cancellation token
            cts.Cancel();
            record.CancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        // Execute LogoutAllCommand with caller's token
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(user.UserId)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await scope.Sender.Send(new LogoutAllCommand(), cts.Token);
            });
        }

        // Even though eviction threw OperationCanceledException after commit, the database changes remain committed
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int updatedSecurityVersion = await dbContext.Users
                .Where(u => u.Id == user.UserId)
                .Select(u => u.TokenSecurityVersion)
                .FirstAsync();
            Assert.Equal(initialSecurityVersion + 1, updatedSecurityVersion);

            bool anyUnrevoked = await dbContext.RefreshTokens
                .AnyAsync(t => t.UserId == user.UserId && t.RevokedAt == null);
            Assert.False(anyUnrevoked);
        });
    }
}
