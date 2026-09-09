using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Quizzes.CreateQuiz;

public sealed record CreateQuizCommand(string Title, string? Description) : ICommand<Guid>;
