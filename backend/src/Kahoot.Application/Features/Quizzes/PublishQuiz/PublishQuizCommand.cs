using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Quizzes.PublishQuiz;

public sealed record PublishQuizCommand(Guid QuizId) : ICommand<PublishQuizResponse>;
