using FluentValidation.Results;
using Kahoot.Application.Features.Games.AdvanceQuestion;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndGame;
using Kahoot.Application.Features.Games.EndQuestion;
using Kahoot.Application.Features.Games.RemoveParticipant;
using Kahoot.Application.Features.Games.ShowLeaderboard;
using Kahoot.Application.Features.Games.StartGame;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Games;

public sealed class GameLifecycleCommandValidatorTests
{
    [Fact]
    public void CreateGameCommandValidator_WithValidQuizId_Succeeds()
    {
        CreateGameCommandValidator validator = new();
        CreateGameCommand command = new(Guid.NewGuid());

        ValidationResult result = validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void CreateGameCommandValidator_WhenQuizIdIsEmpty_Fails()
    {
        CreateGameCommandValidator validator = new();
        CreateGameCommand command = new(Guid.Empty);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }

    [Theory]
    [InlineData("StartGame")]
    [InlineData("EndQuestion")]
    [InlineData("ShowLeaderboard")]
    [InlineData("AdvanceQuestion")]
    [InlineData("EndGame")]
    public void LifecycleValidators_WithValidParameters_Succeed(string commandType)
    {
        Guid gameId = Guid.NewGuid();
        Guid commandId = Guid.NewGuid();
        long version = 1;

        ValidationResult result = commandType switch
        {
            "StartGame" => new StartGameCommandValidator().Validate(new StartGameCommand(gameId, commandId, version)),
            "EndQuestion" => new EndQuestionCommandValidator().Validate(new EndQuestionCommand(gameId, commandId, version)),
            "ShowLeaderboard" => new ShowLeaderboardCommandValidator().Validate(new ShowLeaderboardCommand(gameId, commandId, version)),
            "AdvanceQuestion" => new AdvanceQuestionCommandValidator().Validate(new AdvanceQuestionCommand(gameId, commandId, version)),
            "EndGame" => new EndGameCommandValidator().Validate(new EndGameCommand(gameId, commandId, version)),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType))
        };

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("StartGame")]
    [InlineData("EndQuestion")]
    [InlineData("ShowLeaderboard")]
    [InlineData("AdvanceQuestion")]
    [InlineData("EndGame")]
    public void LifecycleValidators_WhenGameIdIsEmpty_FailWithRequiredMessage(string commandType)
    {
        Guid emptyGameId = Guid.Empty;
        Guid commandId = Guid.NewGuid();
        long version = 1;

        ValidationResult result = commandType switch
        {
            "StartGame" => new StartGameCommandValidator().Validate(new StartGameCommand(emptyGameId, commandId, version)),
            "EndQuestion" => new EndQuestionCommandValidator().Validate(new EndQuestionCommand(emptyGameId, commandId, version)),
            "ShowLeaderboard" => new ShowLeaderboardCommandValidator().Validate(new ShowLeaderboardCommand(emptyGameId, commandId, version)),
            "AdvanceQuestion" => new AdvanceQuestionCommandValidator().Validate(new AdvanceQuestionCommand(emptyGameId, commandId, version)),
            "EndGame" => new EndGameCommandValidator().Validate(new EndGameCommand(emptyGameId, commandId, version)),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType))
        };

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "GameId is required.");
    }

    [Theory]
    [InlineData("StartGame")]
    [InlineData("EndQuestion")]
    [InlineData("ShowLeaderboard")]
    [InlineData("AdvanceQuestion")]
    [InlineData("EndGame")]
    public void LifecycleValidators_WhenCommandIdIsEmpty_FailWithRequiredMessage(string commandType)
    {
        Guid gameId = Guid.NewGuid();
        Guid emptyCommandId = Guid.Empty;
        long version = 1;

        ValidationResult result = commandType switch
        {
            "StartGame" => new StartGameCommandValidator().Validate(new StartGameCommand(gameId, emptyCommandId, version)),
            "EndQuestion" => new EndQuestionCommandValidator().Validate(new EndQuestionCommand(gameId, emptyCommandId, version)),
            "ShowLeaderboard" => new ShowLeaderboardCommandValidator().Validate(new ShowLeaderboardCommand(gameId, emptyCommandId, version)),
            "AdvanceQuestion" => new AdvanceQuestionCommandValidator().Validate(new AdvanceQuestionCommand(gameId, emptyCommandId, version)),
            "EndGame" => new EndGameCommandValidator().Validate(new EndGameCommand(gameId, emptyCommandId, version)),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType))
        };

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "CommandId is required.");
    }

    [Theory]
    [InlineData("StartGame", 0)]
    [InlineData("StartGame", -1)]
    [InlineData("EndQuestion", 0)]
    [InlineData("EndQuestion", -5)]
    [InlineData("ShowLeaderboard", 0)]
    [InlineData("AdvanceQuestion", 0)]
    [InlineData("EndGame", 0)]
    public void LifecycleValidators_WhenExpectedStateVersionLessThanOne_FailWithVersionMessage(
        string commandType,
        long invalidVersion)
    {
        Guid gameId = Guid.NewGuid();
        Guid commandId = Guid.NewGuid();

        ValidationResult result = commandType switch
        {
            "StartGame" => new StartGameCommandValidator().Validate(new StartGameCommand(gameId, commandId, invalidVersion)),
            "EndQuestion" => new EndQuestionCommandValidator().Validate(new EndQuestionCommand(gameId, commandId, invalidVersion)),
            "ShowLeaderboard" => new ShowLeaderboardCommandValidator().Validate(new ShowLeaderboardCommand(gameId, commandId, invalidVersion)),
            "AdvanceQuestion" => new AdvanceQuestionCommandValidator().Validate(new AdvanceQuestionCommand(gameId, commandId, invalidVersion)),
            "EndGame" => new EndGameCommandValidator().Validate(new EndGameCommand(gameId, commandId, invalidVersion)),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType))
        };

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "ExpectedStateVersion must be greater than or equal to 1.");
    }

    [Fact]
    public void RemoveParticipantCommandValidator_WithValidIdentifiers_Succeeds()
    {
        RemoveParticipantCommandValidator validator = new();
        RemoveParticipantCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        ValidationResult result = validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void RemoveParticipantCommandValidator_WhenGameIdIsEmpty_Fails()
    {
        RemoveParticipantCommandValidator validator = new();
        RemoveParticipantCommand command = new(Guid.Empty, Guid.NewGuid());

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "GameId is required.");
    }

    [Fact]
    public void RemoveParticipantCommandValidator_WhenParticipantIdIsEmpty_Fails()
    {
        RemoveParticipantCommandValidator validator = new();
        RemoveParticipantCommand command = new(Guid.NewGuid(), Guid.Empty);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "ParticipantId is required.");
    }
}
