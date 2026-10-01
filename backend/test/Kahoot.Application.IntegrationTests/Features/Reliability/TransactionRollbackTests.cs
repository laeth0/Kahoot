namespace Kahoot.Application.IntegrationTests.Features.Reliability;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth.Register;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Application.IntegrationTests.TestSupport.Concurrency;
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
public sealed class TransactionRollbackTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public TransactionRollbackTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateQuestion_BeforeCommitFailure_RestoresDeletedAndInsertedChoices()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RollbackHost1", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Rollback Quiz 1", questionCount: 1);

        // Fetch the existing question and choices
        Question originalQuestion = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.Questions
                .FirstAsync(q => q.QuizId == quiz.QuizId);
        });

        long originalRevision = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.Quizzes
                .Where(q => q.Id == quiz.QuizId)
                .Select(q => q.Revision)
                .FirstAsync();
        });

        List<Guid> originalChoiceIds = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.Choices
                .Where(c => c.QuestionId == originalQuestion.Id)
                .Select(c => c.Id)
                .ToListAsync();
        });
        Assert.NotEmpty(originalChoiceIds);

        // Create an update command with completely new choices
        List<ChoiceRequest> newChoices = new()
        {
            new ChoiceRequest("Replacement Choice 1", true),
            new ChoiceRequest("Replacement Choice 2", false),
            new ChoiceRequest("Replacement Choice 3", false),
            new ChoiceRequest("Replacement Choice 4", false)
        };

        UpdateQuestionCommand updateCmd = new(
            quiz.QuizId,
            originalQuestion.Id,
            "Updated Text Attempt",
            ImageId: null,
            DurationSeconds: 45,
            BasePoints: 2000,
            Choices: newChoices);

        // Execute in scope where pre-commit failure is injected
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            scope.Services.GetRequiredService<ScopeConcurrencyGate>().FailBeforeCommit = true;

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await scope.Sender.Send(updateCmd);
            });
        }

        // Verify in database: transaction rolled back completely, original state intact
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Quiz currentQuiz = await dbContext.Quizzes.FirstAsync(q => q.Id == quiz.QuizId);
            Assert.Equal(originalRevision, currentQuiz.Revision);

            Question currentQuestion = await dbContext.Questions
                .FirstAsync(q => q.Id == originalQuestion.Id);

            Assert.Equal(originalQuestion.Text, currentQuestion.Text);
            Assert.Equal(originalQuestion.DurationSeconds, currentQuestion.DurationSeconds);
            Assert.Equal(originalQuestion.BasePoints, currentQuestion.BasePoints);

            List<Guid> currentChoiceIds = await dbContext.Choices
                .Where(c => c.QuestionId == originalQuestion.Id)
                .Select(c => c.Id)
                .OrderBy(id => id)
                .ToListAsync();
            List<Guid> expectedChoiceIds = originalChoiceIds.OrderBy(id => id).ToList();
            Assert.Equal(expectedChoiceIds, currentChoiceIds);

            // Verify none of the replacement choices exist
            bool anyNewChoiceExists = await dbContext.Choices
                .AnyAsync(c => c.Text == "Replacement Choice 1" || c.Text == "Replacement Choice 2");
            Assert.False(anyNewChoiceExists);
        });
    }

    [Fact]
    public async Task StartGame_BeforeCommitFailure_RollsBackStateAndIdempotency()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RollbackHost2", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Rollback Quiz 2", questionCount: 1);

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
        await harness.SendAsync(new JoinGameCommand(createRes.Pin, "Player1", Guid.NewGuid(), "10.0.0.1"), TestCaller.Anonymous);
        await harness.SendAsync(new JoinGameCommand(createRes.Pin, "Player2", Guid.NewGuid(), "10.0.0.2"), TestCaller.Anonymous);

        Guid startCommandId = Guid.NewGuid();
        StartGameCommand startCmd = new(createRes.GameId, startCommandId, ExpectedStateVersion: 1);

        // Execute start game with pre-commit failure injection
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            scope.Services.GetRequiredService<ScopeConcurrencyGate>().FailBeforeCommit = true;

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await scope.Sender.Send(startCmd);
            });
        }

        // Verify in database: Game is still in Lobby, StateVersion is still 1, no idempotency row
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(GameStatus.Lobby, game.Status);
            Assert.Equal(1, game.StateVersion);

            bool idempotencyExists = await dbContext.GameCommandIdempotencies
                .AnyAsync(i => i.GameId == createRes.GameId && i.CommandId == startCommandId);
            Assert.False(idempotencyExists);
        });

        // Verify no notification was published
        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();
        Assert.DoesNotContain(notificationService.Records, r => r.GameId == createRes.GameId && r.EventName == "QuestionStarted");
    }

    [Fact]
    public async Task UniqueConstraintViolation_RollsBackCleanlyAndSubsequentRequestsSucceed()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        RegisterCommand firstUser = new("conflict_user", "Password123!");
        Result<RegisterResponse> res1 = await harness.SendAsync(firstUser, TestCaller.Anonymous);
        Assert.True(res1.IsSuccess);

        // Attempt duplicate registration
        RegisterCommand dupUser = new("conflict_user", "Password123!");
        Result<RegisterResponse> res2 = await harness.SendAsync(dupUser, TestCaller.Anonymous);
        Assert.True(res2.IsFailure);

        // Ensure database state is consistent and subsequent registration succeeds cleanly
        RegisterCommand secondUser = new("other_user", "Password123!");
        Result<RegisterResponse> res3 = await harness.SendAsync(secondUser, TestCaller.Anonymous);
        Assert.True(res3.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int userCount = await dbContext.Users.CountAsync(u => u.NormalizedUsername == "CONFLICT_USER");
            Assert.Equal(1, userCount);
        });
    }
}
