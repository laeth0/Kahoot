using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Quizzes.DeleteQuiz;

public sealed record DeleteQuizCommand(Guid QuizId) : ICommand;
