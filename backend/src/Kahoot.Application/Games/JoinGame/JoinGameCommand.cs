using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.JoinGame;

public sealed record JoinGameCommand(string Pin, string Nickname) : ICommand<JoinGameResponse>;
