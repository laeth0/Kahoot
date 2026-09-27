using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Quizzes.DeleteQuiz;

public sealed record DeleteQuizCommand(Guid QuizId) : ICommand;
