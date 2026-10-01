namespace Kahoot.Application.IntegrationTests.Features.Games.Creation;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "07")]
public sealed class GameCreationTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public GameCreationTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateGame_PopulatedOwnedQuiz_PersistsCompleteLobbySnapshot()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "GameHost1");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            host.UserId,
            title: "Planets Quiz",
            questionCount: 2,
            choicesPerQuestion: 4);

        CreateGameCommand command = new(quiz.QuizId);
        Result<CreateGameResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        CreateGameResponse response = result.Value;
        Assert.NotEqual(Guid.Empty, response.GameId);
        Assert.Equal("Planets Quiz", response.Title);
        Assert.Equal("LOBBY", response.Status);
        Assert.Equal(1, response.StateVersion);
        Assert.Equal(2, response.TotalQuestions);
        Assert.Equal(8, response.Pin.Length);
        Assert.Equal($"https://client.kahoot.test/join/{response.Pin}", response.JoinUrl);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game? persistedGame = await dbContext.Games
                .FirstOrDefaultAsync(g => g.Id == response.GameId);

            Assert.NotNull(persistedGame);
            Assert.Equal(host.UserId, persistedGame.HostAccountId);
            Assert.Equal(quiz.QuizId, persistedGame.SourceQuizId);
            Assert.Equal(GameStatus.Lobby, persistedGame.Status);
            Assert.Equal(1, persistedGame.StateVersion);
            Assert.Equal(0, persistedGame.PresenceVersion);
            Assert.Null(persistedGame.CurrentQuestionIndex);
            Assert.Equal(1, persistedGame.NextSeatNumber);
            Assert.Equal(0, persistedGame.ReservedParticipantCount);
            Assert.False(persistedGame.IsTerminatedBySuspension);
            Assert.Null(persistedGame.FinishedAt);

            List<GameQuestionSnapshot> questionSnapshots = await dbContext.GameQuestionSnapshots
                .Where(qs => qs.GameId == response.GameId)
                .OrderBy(qs => qs.OrderIndex)
                .ToListAsync();

            Assert.Equal(2, questionSnapshots.Count);
            Assert.Equal(1, questionSnapshots[0].OrderIndex);
            Assert.Equal(2, questionSnapshots[1].OrderIndex);
            Assert.Equal("Question 1 Text", questionSnapshots[0].Text);
            Assert.Equal(30, questionSnapshots[0].DurationSeconds);
            Assert.Equal(1000, questionSnapshots[0].BasePoints);
            Assert.Equal(0, questionSnapshots[0].AcceptedAnswerCount);

            List<Guid> snapshotQuestionIds = questionSnapshots.Select(qs => qs.Id).ToList();
            List<GameChoiceSnapshot> choiceSnapshots = await dbContext.GameChoiceSnapshots
                .Where(cs => snapshotQuestionIds.Contains(cs.GameQuestionId))
                .OrderBy(cs => cs.GameQuestionId)
                .ThenBy(cs => cs.OrderIndex)
                .ToListAsync();

            Assert.Equal(8, choiceSnapshots.Count);
            List<GameChoiceSnapshot> firstQuestionChoices = choiceSnapshots
                .Where(cs => cs.GameQuestionId == questionSnapshots[0].Id)
                .OrderBy(cs => cs.OrderIndex)
                .ToList();

            Assert.Equal(4, firstQuestionChoices.Count);
            Assert.Equal(1, firstQuestionChoices[0].OrderIndex);
            Assert.Equal(2, firstQuestionChoices[1].OrderIndex);
            Assert.Equal(3, firstQuestionChoices[2].OrderIndex);
            Assert.Equal(4, firstQuestionChoices[3].OrderIndex);
            Assert.True(firstQuestionChoices[0].IsCorrect);
            Assert.False(firstQuestionChoices[1].IsCorrect);
            Assert.Equal(0, firstQuestionChoices[0].SelectionCount);

            List<Question> sourceQuestions = await dbContext.Questions
                .Where(q => q.QuizId == quiz.QuizId)
                .ToListAsync();

            List<Guid> sourceQuestionIds = sourceQuestions.Select(q => q.Id).ToList();
            foreach (GameQuestionSnapshot questionSnapshot in questionSnapshots)
            {
                Assert.DoesNotContain(questionSnapshot.Id, sourceQuestionIds);
            }
        });
    }

    [Fact]
    public async Task CreateGame_EmptyQuiz_FailsValidationWithoutPersistingGame()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "GameHostEmpty");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Empty Quiz");

        CreateGameCommand command = new(quiz.QuizId);
        Result<CreateGameResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Failed", result.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int gameCount = await dbContext.Games.CountAsync(g => g.SourceQuizId == quiz.QuizId);
            Assert.Equal(0, gameCount);

            int snapshotCount = await dbContext.GameQuestionSnapshots.CountAsync(qs => qs.HostAccountId == host.UserId);
            Assert.Equal(0, snapshotCount);
        });
    }

    [Fact]
    public async Task CreateGame_ForeignQuiz_ReturnsQuizNotFound()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "HostOwnerA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "HostOwnerB");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, hostA.UserId, "Quiz of Host A");

        CreateGameCommand command = new(quiz.QuizId);
        Result<CreateGameResponse> result = await harness.SendAsync(command, TestCaller.Host(hostB.UserId));

        Assert.True(result.IsFailure);
        Assert.Equal(QuizErrors.NotFound.Code, result.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int gameCount = await dbContext.Games.CountAsync(g => g.SourceQuizId == quiz.QuizId);
            Assert.Equal(0, gameCount);
        });
    }

    [Fact]
    public async Task CreateGame_AnonymousOrSuspendedHost_ReturnsUnauthorized()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ActiveHostForQuiz");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Valid Populated Quiz");

        CreateGameCommand command = new(quiz.QuizId);

        Result<CreateGameResponse> anonResult = await harness.SendAsync(command, TestCaller.Anonymous);
        Assert.True(anonResult.IsFailure);
        Assert.Equal(AuthErrors.Unauthorized.Code, anonResult.Error.Code);

        TestUserRecord suspendedHost = await FeatureData.CreateUserAsync(
            harness,
            "SuspendedHostUser",
            status: UserStatus.Suspended);

        Result<CreateGameResponse> suspendedResult = await harness.SendAsync(command, TestCaller.Host(suspendedHost.UserId));
        Assert.True(suspendedResult.IsFailure);
        Assert.Equal(AuthErrors.Unauthorized.Code, suspendedResult.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int gameCount = await dbContext.Games.CountAsync(g => g.SourceQuizId == quiz.QuizId);
            Assert.Equal(0, gameCount);
        });
    }

    [Fact]
    public async Task CreateGame_SnapshotIndependence_EditingSourceQuizDoesNotAlterSnapshot()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "SnapshotIsolationHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            host.UserId,
            title: "Original Title",
            questionCount: 1,
            choicesPerQuestion: 2);

        CreateGameCommand command = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(command, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        // End the created game so it is not active
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Game game = await scope.DbContext.Games.FirstAsync(g => g.Id == gameId);
            game.Status = GameStatus.Finished;
            game.FinishedAt = DateTimeOffset.UtcNow;
            await scope.DbContext.SaveChangesAsync();
        }

        // Now modify source quiz and question
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Quiz sourceQuiz = await scope.DbContext.Quizzes.FirstAsync(q => q.Id == quiz.QuizId);
            sourceQuiz.Title = "Mutated Title";

            Question sourceQuestion = await scope.DbContext.Questions.FirstAsync(q => q.QuizId == quiz.QuizId);
            sourceQuestion.Text = "Mutated Question Text";

            Choice sourceChoice = await scope.DbContext.Choices.FirstAsync(c => c.QuestionId == sourceQuestion.Id);
            sourceChoice.Text = "Mutated Choice Text";
            sourceChoice.IsCorrect = !sourceChoice.IsCorrect;

            await scope.DbContext.SaveChangesAsync();
        }

        // Assert game snapshot was completely unaffected
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal("Original Title", game.Title);

            GameQuestionSnapshot questionSnapshot = await dbContext.GameQuestionSnapshots.FirstAsync(qs => qs.GameId == gameId);
            Assert.Equal("Question 1 Text", questionSnapshot.Text);

            List<GameChoiceSnapshot> choiceSnapshots = await dbContext.GameChoiceSnapshots
                .Where(cs => cs.GameQuestionId == questionSnapshot.Id)
                .OrderBy(cs => cs.OrderIndex)
                .ToListAsync();

            Assert.Equal("Choice 1", choiceSnapshots[0].Text);
            Assert.True(choiceSnapshots[0].IsCorrect);
        });
    }
}
