namespace Kahoot.Application.IntegrationTests.TestSupport;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

public sealed record TestUserRecord(
    Guid UserId,
    string DisplayUsername,
    string NormalizedUsername,
    string Password,
    string PasswordHash,
    UserRole Role,
    UserStatus Status);

public sealed record TestQuizRecord(
    Guid QuizId,
    Guid HostAccountId,
    string Title,
    string? Description,
    long Revision);

public static class FeatureData
{
    public static async Task<TestUserRecord> CreateUserAsync(
        ApplicationTestHarness harness,
        string displayUsername = "TestHost",
        string password = "Password123!@#",
        UserRole role = UserRole.Host,
        UserStatus status = UserStatus.Active,
        CancellationToken cancellationToken = default)
    {
        IPasswordHasher passwordHasher = harness.Services.GetRequiredService<IPasswordHasher>();
        string passwordHash = await passwordHasher.HashPasswordAsync(password, cancellationToken);
        Guid userId = Guid.NewGuid();
        string normalizedUsername = displayUsername.ToUpperInvariant();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        User user = new()
        {
            Id = userId,
            DisplayUsername = displayUsername,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash,
            Role = role,
            Status = status,
            TokenSecurityVersion = 1,
            Revision = 1,
            TerminationPending = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Anonymous))
        {
            scope.DbContext.Users.Add(user);
            await scope.DbContext.SaveChangesAsync(cancellationToken);
        }

        return new TestUserRecord(
            userId,
            displayUsername,
            normalizedUsername,
            password,
            passwordHash,
            role,
            status);
    }

    public static async Task<TestQuizRecord> CreateQuizAsync(
        ApplicationTestHarness harness,
        Guid hostAccountId,
        string title = "Integration Quiz",
        string? description = "Quiz Description",
        CancellationToken cancellationToken = default)
    {
        Guid quizId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Quiz quiz = new()
        {
            Id = quizId,
            HostAccountId = hostAccountId,
            Title = title,
            Description = description,
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = hostAccountId,
            UpdatedBy = hostAccountId
        };

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostAccountId)))
        {
            scope.DbContext.Quizzes.Add(quiz);
            await scope.DbContext.SaveChangesAsync(cancellationToken);
        }

        return new TestQuizRecord(quizId, hostAccountId, title, description, 1);
    }

    public static async Task<TestQuizRecord> CreatePopulatedQuizAsync(
        ApplicationTestHarness harness,
        Guid hostAccountId,
        string title = "Populated Quiz",
        int questionCount = 2,
        int choicesPerQuestion = 4,
        CancellationToken cancellationToken = default)
    {
        TestQuizRecord quiz = await CreateQuizAsync(harness, hostAccountId, title, cancellationToken: cancellationToken);

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostAccountId)))
        {
            for (int questionIndex = 0; questionIndex < questionCount; questionIndex++)
            {
                Guid questionId = Guid.NewGuid();
                Question question = new()
                {
                    Id = questionId,
                    HostAccountId = hostAccountId,
                    QuizId = quiz.QuizId,
                    OrderIndex = questionIndex,
                    Text = $"Question {questionIndex + 1} Text",
                    DurationSeconds = 30,
                    BasePoints = 1000
                };
                scope.DbContext.Questions.Add(question);

                for (int choiceIndex = 0; choiceIndex < choicesPerQuestion; choiceIndex++)
                {
                    Choice choice = new()
                    {
                        Id = Guid.NewGuid(),
                        HostAccountId = hostAccountId,
                        QuestionId = questionId,
                        OrderIndex = choiceIndex,
                        Text = $"Choice {choiceIndex + 1}",
                        IsCorrect = choiceIndex == 0
                    };
                    scope.DbContext.Choices.Add(choice);
                }
            }

            await scope.DbContext.SaveChangesAsync(cancellationToken);
        }

        return quiz;
    }

    public static async Task<Kahoot.Application.Features.Auth.Login.LoginResult> LoginAsync(
        ApplicationTestHarness harness,
        string username,
        string password,
        string ipAddress = "127.0.0.1",
        CancellationToken cancellationToken = default)
    {
        Kahoot.Application.Features.Auth.Login.LoginCommand command = new(username, password, ipAddress);
        Kahoot.Application.Common.Results.Result<Kahoot.Application.Features.Auth.Login.LoginResult> result = await harness.SendAsync(command, TestCaller.Anonymous, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Login failed for {username}: {result.Error.Code}");
        }

        return result.Value;
    }
}

