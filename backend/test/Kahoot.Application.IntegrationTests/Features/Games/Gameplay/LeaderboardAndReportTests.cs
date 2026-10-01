namespace Kahoot.Application.IntegrationTests.Features.Games.Gameplay;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndGame;
using Kahoot.Application.Features.Games.EndQuestion;
using Kahoot.Application.Features.Games.GetGameReport;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Application.Features.Games.ShowLeaderboard;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Application.Features.Games.SubmitAnswer;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "10")]
public sealed class LeaderboardAndReportTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public LeaderboardAndReportTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Leaderboard_UsesDatabaseOrderingAndExcludesRemovedPlayers()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LeaderboardHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            host.UserId,
            title: "Ranking Quiz",
            questionCount: 1,
            choicesPerQuestion: 4);

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerTop", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "PlayerSecond", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        JoinGameCommand join3 = new(pin, "PlayerRemoved", Guid.NewGuid(), "192.168.1.3");
        Result<JoinGameResponse> join3Result = await harness.SendAsync(join3, TestCaller.Anonymous);
        Assert.True(join3Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;

        // Player 1 answers correctly
        SubmitAnswerCommand submit1 = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);
        await harness.SendAsync(submit1, TestCaller.Anonymous);

        // Mark Player 3 as removed
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Participant p3 = await scope.DbContext.Participants.FirstAsync(pt => pt.Id == join3Result.Value.ParticipantId);
            p3.IsRemoved = true;
            p3.RemovedAt = DateTimeOffset.UtcNow;
            await scope.DbContext.SaveChangesAsync();
        }

        // End question
        EndQuestionCommand endCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 2);
        Result<EndQuestionResponse> endResult = await harness.SendAsync(endCommand, TestCaller.Host(host.UserId));
        Assert.True(endResult.IsSuccess);

        // Show leaderboard
        ShowLeaderboardCommand showLbCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 3);
        Result<ShowLeaderboardResponse> lbResult = await harness.SendAsync(showLbCommand, TestCaller.Host(host.UserId));
        Assert.True(lbResult.IsSuccess);

        ShowLeaderboardResponse response = lbResult.Value;
        Assert.Equal(2, response.TopParticipants.Count);
        Assert.Equal(join1Result.Value.ParticipantId, response.TopParticipants[0].ParticipantId);
        Assert.Equal(1, response.TopParticipants[0].Rank);
        Assert.True(response.TopParticipants[0].TotalScore > 0);

        Assert.Equal(join2Result.Value.ParticipantId, response.TopParticipants[1].ParticipantId);
        Assert.Equal(2, response.TopParticipants[1].Rank);
        Assert.Equal(0, response.TopParticipants[1].TotalScore);

        Assert.DoesNotContain(response.TopParticipants, p => p.ParticipantId == join3Result.Value.ParticipantId);
    }

    [Fact]
    public async Task EndGameAndReport_UseCommittedSnapshotAndAnswerAggregates()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "ReportHostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "ReportHostB");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            hostA.UserId,
            title: "Comprehensive Quiz",
            questionCount: 1,
            choicesPerQuestion: 4);

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(hostA.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PodiumGold", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "PodiumSilver", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        // Before game ends, querying report must fail with InvalidStateTransition
        GetGameReportQuery preEndQuery = new(gameId);
        Result<GetGameReportResponse> preEndResult = await harness.SendAsync(preEndQuery, TestCaller.Host(hostA.UserId));
        Assert.True(preEndResult.IsFailure);
        Assert.Equal(GameErrors.InvalidStateTransition.Code, preEndResult.Error.Code);

        // Start game and answer
        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(hostA.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;

        SubmitAnswerCommand submit = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);
        await harness.SendAsync(submit, TestCaller.Anonymous);

        EndQuestionCommand endQuestion = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 2);
        await harness.SendAsync(endQuestion, TestCaller.Host(hostA.UserId));

        // End Game
        EndGameCommand endGame = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 3);
        Result<EndGameResponse> endGameResult = await harness.SendAsync(endGame, TestCaller.Host(hostA.UserId));
        Assert.True(endGameResult.IsSuccess);

        // Query Report as Host A
        GetGameReportQuery reportQuery = new(gameId);
        Result<GetGameReportResponse> reportResult = await harness.SendAsync(reportQuery, TestCaller.Host(hostA.UserId));
        Assert.True(reportResult.IsSuccess);

        GetGameReportResponse report = reportResult.Value;
        Assert.Equal(gameId, report.GameId);
        Assert.Equal("Comprehensive Quiz", report.Title);
        Assert.Equal("FINISHED", report.Status);
        Assert.Equal(1, report.TotalQuestions);
        Assert.Equal(2, report.TotalParticipants);
        Assert.NotNull(report.FinishedAt);
        Assert.Single(report.Questions);
        Assert.Equal(2, report.Leaderboard.Count);
        Assert.Equal(1, report.Leaderboard[0].Rank);
        Assert.Equal(2, report.Leaderboard[1].Rank);

        // Foreign Host B querying report fails with NotFound
        Result<GetGameReportResponse> foreignReportResult = await harness.SendAsync(reportQuery, TestCaller.Host(hostB.UserId));
        Assert.True(foreignReportResult.IsFailure);
        Assert.Equal(GameErrors.NotFound.Code, foreignReportResult.Error.Code);
    }
}
