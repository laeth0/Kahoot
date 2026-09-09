using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Quizzes.UpdateQuiz;

public sealed record UpdateQuizCommand(Guid QuizId, string Title, string? Description) : ICommand;
