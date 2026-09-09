using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Quizzes.Questions.DeleteQuestion;

public sealed record DeleteQuestionCommand(Guid QuizId, Guid QuestionId) : ICommand;
