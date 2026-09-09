using Kahoot.Domain.Common;

namespace Kahoot.Application.Games.Common;

public static class GameErrors
{
    public static readonly Error NotFound =
        new("Game.NotFound", "The game was not found.");

    public static readonly Error QuizNotPublished =
        new("Game.QuizNotPublished", "A game can only be started from a published quiz.");

    public static readonly Error PinUnavailable =
        new("Game.PinUnavailable", "Could not allocate a unique game PIN. Please retry.");

    public static readonly Error InvalidPin =
        new("Game.InvalidPin", "No open game was found for that PIN.");

    public static readonly Error NotJoinable =
        new("Game.NotJoinable", "This game is no longer accepting players.");

    public static readonly Error NicknameTaken =
        new("Game.NicknameTaken", "That nickname is already taken in this game.");

    public static readonly Error InvalidStateTransition =
        new("Game.InvalidStateTransition", "That action is not valid for the current game state.");

    public static readonly Error NoMoreQuestions =
        new("Game.NoMoreQuestions", "There are no more questions; end the game to see final results.");

    public static readonly Error ConcurrentModification =
        new("Game.ConcurrentModification", "The game changed while processing the request. Please retry.");

    public static readonly Error ParticipantNotFound =
        new("Game.ParticipantNotFound", "The player was not found in this game.");

    public static readonly Error ParticipantRemoved =
        new("Game.ParticipantRemoved", "This player has been removed from the game.");

    public static readonly Error InvalidSessionToken =
        new("Game.InvalidSessionToken", "The player session token is invalid.");

    public static readonly Error QuestionNotActive =
        new("Game.QuestionNotActive", "There is no question open for answers right now.");

    public static readonly Error NotCurrentQuestion =
        new("Game.NotCurrentQuestion", "That question is not the one currently open.");

    public static readonly Error QuestionClosed =
        new("Game.QuestionClosed", "The answer window for this question has closed.");

    public static readonly Error ChoiceNotInQuestion =
        new("Game.ChoiceNotInQuestion", "The selected choice does not belong to the current question.");
}
