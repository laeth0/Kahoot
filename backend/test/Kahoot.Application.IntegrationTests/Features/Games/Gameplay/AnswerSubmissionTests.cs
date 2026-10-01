namespace Kahoot.Application.IntegrationTests.Features.Games.Gameplay;

using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Application.Features.Games.SubmitAnswer;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "10")]
public sealed class AnswerSubmissionTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public AnswerSubmissionTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Submit_ValidTokenHash_PersistsOneAnswerAndDistinctSelections()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnswerHost1");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            host.UserId,
            title: "Trivia Quiz",
            questionCount: 1,
            choicesPerQuestion: 4);

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand joinCommand = new(pin, "PlayerAnswerer", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);
        Guid participantId = joinResult.Value.ParticipantId;
        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(joinResult.Value.PlayerSessionToken));

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;

        SubmitAnswerCommand submitCommand = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-123",
            SessionTokenHash: tokenHash,
            ConnectionGeneration: null);

        Result<SubmitAnswerResponse> submitResult = await harness.SendAsync(submitCommand, TestCaller.Anonymous);
        Assert.True(submitResult.IsSuccess);
        Assert.True(submitResult.Value.Accepted);
        Assert.False(submitResult.Value.AlreadyAnswered);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            List<AnswerSubmission> answers = await dbContext.AnswerSubmissions
                .Where(a => a.GameId == gameId && a.ParticipantId == participantId)
                .ToListAsync();

            Assert.Single(answers);
            AnswerSubmission answer = answers[0];
            Assert.Equal(questionId, answer.GameQuestionId);
            Assert.True(answer.IsCorrect);
            Assert.True(answer.PointsAwarded > 0);
            Assert.True(answer.ResponseTimeMs >= 0);

            List<AnswerSubmissionChoice> choices = await dbContext.AnswerSubmissionChoices
                .Where(c => c.AnswerSubmissionId == answer.Id)
                .ToListAsync();

            Assert.Single(choices);
            Assert.Equal(correctChoiceId, choices[0].GameChoiceId);

            Participant? participant = await dbContext.Participants
                .FirstOrDefaultAsync(p => p.Id == participantId);
            Assert.NotNull(participant);
            Assert.Equal(answer.PointsAwarded, participant.TotalScore);

            GameQuestionSnapshot? snapshot = await dbContext.GameQuestionSnapshots
                .FirstOrDefaultAsync(q => q.Id == questionId);
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.AcceptedAnswerCount);
        });
    }

    [Fact]
    public async Task Submit_ValidGeneration_UsesCurrentParticipantBinding()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnswerHost2");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Generation Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand joinCommand = new(pin, "GenPlayer", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);
        Guid participantId = joinResult.Value.ParticipantId;

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;

        // 1. Both SessionTokenHash and ConnectionGeneration null => pipeline ValidationException
        SubmitAnswerCommand missingBoth = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: null);

        await Assert.ThrowsAsync<ValidationException>(async () =>
            await harness.SendAsync(missingBoth, TestCaller.Anonymous));

        // 2. Stale connection generation => InvalidSessionToken
        SubmitAnswerCommand staleGen = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 999);

        Result<SubmitAnswerResponse> staleResult = await harness.SendAsync(staleGen, TestCaller.Anonymous);
        Assert.True(staleResult.IsFailure);
        Assert.Equal(GameErrors.InvalidSessionToken.Code, staleResult.Error.Code);

        // 3. Current connection generation (1) => Success
        SubmitAnswerCommand validGen = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> validResult = await harness.SendAsync(validGen, TestCaller.Anonymous);
        Assert.True(validResult.IsSuccess);
        Assert.True(validResult.Value.Accepted);
    }

    [Fact]
    public async Task Submit_ExactCorrectSet_GradesAndPersistsScore()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnswerHost3");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Grading Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerCorrect", Guid.NewGuid(), "192.168.1.10");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "PlayerWrong", Guid.NewGuid(), "192.168.1.11");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;
        Guid wrongChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == false).ChoiceId;

        // Player 1 submits with duplicate choice IDs in list: [correctChoiceId, correctChoiceId]
        // The handler deduplicates distinct choices.
        SubmitAnswerCommand submit1 = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { correctChoiceId, correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> result1 = await harness.SendAsync(submit1, TestCaller.Anonymous);
        Assert.True(result1.IsSuccess);

        // Player 2 submits wrong choice
        SubmitAnswerCommand submit2 = new(
            gameId,
            join2Result.Value.ParticipantId,
            questionId,
            new List<Guid> { wrongChoiceId },
            ConnectionId: "conn-2",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> result2 = await harness.SendAsync(submit2, TestCaller.Anonymous);
        Assert.True(result2.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            AnswerSubmission ans1 = await dbContext.AnswerSubmissions
                .FirstAsync(a => a.ParticipantId == join1Result.Value.ParticipantId);
            Assert.True(ans1.IsCorrect);
            Assert.True(ans1.PointsAwarded > 0);

            int choicesForAns1 = await dbContext.AnswerSubmissionChoices
                .CountAsync(c => c.AnswerSubmissionId == ans1.Id);
            Assert.Equal(1, choicesForAns1);

            AnswerSubmission ans2 = await dbContext.AnswerSubmissions
                .FirstAsync(a => a.ParticipantId == join2Result.Value.ParticipantId);
            Assert.False(ans2.IsCorrect);
            Assert.Equal(0, ans2.PointsAwarded);
        });
    }

    [Fact]
    public async Task Submit_UnknownOrForeignChoiceOrQuestion_Rejects()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnswerHost4");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Unknown Choice Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand joinCommand = new(pin, "PlayerForeign", Guid.NewGuid(), "192.168.1.20");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;

        // Foreign choice ID
        SubmitAnswerCommand foreignChoiceCommand = new(
            gameId,
            joinResult.Value.ParticipantId,
            questionId,
            new List<Guid> { Guid.NewGuid() },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> choiceResult = await harness.SendAsync(foreignChoiceCommand, TestCaller.Anonymous);
        Assert.True(choiceResult.IsFailure);
        Assert.Equal(GameErrors.InvalidChoices.Code, choiceResult.Error.Code);

        // Foreign question ID
        SubmitAnswerCommand foreignQuestionCommand = new(
            gameId,
            joinResult.Value.ParticipantId,
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> questionResult = await harness.SendAsync(foreignQuestionCommand, TestCaller.Anonymous);
        Assert.True(questionResult.IsFailure);
        Assert.Equal(GameErrors.NotCurrentQuestion.Code, questionResult.Error.Code);
    }

    [Fact]
    public async Task Submit_RevokedTokenWrongParticipantOrRemovedParticipant_Rejects()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnswerHost5");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Revocation Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand joinCommand = new(pin, "PlayerRevoked", Guid.NewGuid(), "192.168.1.30");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);
        Guid participantId = joinResult.Value.ParticipantId;
        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(joinResult.Value.PlayerSessionToken));

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid choiceId = startResult.Value.CurrentQuestion.Choices[0].ChoiceId;

        // Revoke token in DB
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            ParticipantSessionToken token = await scope.DbContext.ParticipantSessionTokens
                .FirstAsync(t => t.ParticipantId == participantId);
            token.RevokedAt = DateTimeOffset.UtcNow;
            await scope.DbContext.SaveChangesAsync();
        }

        SubmitAnswerCommand revokedCommand = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { choiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: tokenHash,
            ConnectionGeneration: null);

        Result<SubmitAnswerResponse> revokedResult = await harness.SendAsync(revokedCommand, TestCaller.Anonymous);
        Assert.True(revokedResult.IsFailure);
        Assert.Equal(GameErrors.InvalidSessionToken.Code, revokedResult.Error.Code);

        // Mark participant removed -> should reject with GameErrors.ParticipantRemoved
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Participant p = await scope.DbContext.Participants.FirstAsync(pt => pt.Id == participantId);
            p.IsRemoved = true;
            p.RemovedAt = DateTimeOffset.UtcNow;
            await scope.DbContext.SaveChangesAsync();
        }

        SubmitAnswerCommand removedCommand = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { choiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: tokenHash,
            ConnectionGeneration: null);

        Result<SubmitAnswerResponse> removedResult = await harness.SendAsync(removedCommand, TestCaller.Anonymous);
        Assert.True(removedResult.IsFailure);
        Assert.Equal(GameErrors.ParticipantRemoved.Code, removedResult.Error.Code);
    }

    [Fact]
    public async Task Submit_AfterDeadline_RejectsWithoutClosingQuestion()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnswerHost6");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Deadline Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand joinCommand = new(pin, "PlayerLate", Guid.NewGuid(), "192.168.1.40");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);
        Guid participantId = joinResult.Value.ParticipantId;

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid choiceId = startResult.Value.CurrentQuestion.Choices[0].ChoiceId;

        // Set EndsAt to past
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            GameQuestionSnapshot q = await scope.DbContext.GameQuestionSnapshots.FirstAsync(qs => qs.Id == questionId);
            q.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            q.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-4);
            await scope.DbContext.SaveChangesAsync();
        }

        SubmitAnswerCommand lateCommand = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { choiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> lateResult = await harness.SendAsync(lateCommand, TestCaller.Anonymous);
        Assert.True(lateResult.IsFailure);
        Assert.Equal(GameErrors.AnswerTooLate.Code, lateResult.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.QuestionActive, game.Status);
            Assert.Equal(2, game.StateVersion);
        });
    }
}
