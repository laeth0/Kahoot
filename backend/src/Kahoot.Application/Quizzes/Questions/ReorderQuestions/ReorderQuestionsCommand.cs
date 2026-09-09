using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Quizzes.Questions.ReorderQuestions;

public sealed record ReorderQuestionsCommand(Guid QuizId, IReadOnlyList<Guid> OrderedQuestionIds) : ICommand;
