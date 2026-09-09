using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Quizzes.PublishQuiz;

public sealed record PublishQuizCommand(Guid QuizId) : ICommand;
