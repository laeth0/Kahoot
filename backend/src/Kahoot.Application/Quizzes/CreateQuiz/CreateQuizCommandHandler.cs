using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Application.Quizzes.CreateQuiz;

internal sealed class CreateQuizCommandHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<CreateQuizCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateQuizCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<Guid>(SharedErrors.Unauthorized);
        }

        Quiz quiz = new()
        {
            HostId = hostId,
            Title = command.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim()
        };

        dbContext.Quizzes.Add(quiz);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(quiz.Id);
    }
}
