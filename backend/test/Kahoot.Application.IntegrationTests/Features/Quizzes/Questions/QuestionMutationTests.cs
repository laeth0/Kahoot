namespace Kahoot.Application.IntegrationTests.Features.Quizzes.Questions;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.AddQuestion;
using Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;
using Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;
using Kahoot.Application.Features.Quizzes.ReorderQuestions;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Questions")]
[Trait("Phase", "06")]
public sealed class QuestionMutationTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public QuestionMutationTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddQuestion_PersistsOwnedOrderedChoicesAndIncrementsQuizRevision()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AddQHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz for AddQ");

        List<ChoiceRequest> choices = new()
        {
            new("First Choice", true),
            new("Second Choice", false)
        };

        AddQuestionCommand command = new(
            quiz.QuizId,
            "What is 2 + 2?",
            null,
            20,
            1000,
            choices);

        Result<QuestionResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.OrderIndex);
        Assert.Equal("What is 2 + 2?", result.Value.Text);
        Assert.Equal(2, result.Value.Choices.Count);
        Assert.Equal(0, result.Value.Choices[0].OrderIndex);
        Assert.Equal("First Choice", result.Value.Choices[0].Text);
        Assert.True(result.Value.Choices[0].IsCorrect);
        Assert.Equal(1, result.Value.Choices[1].OrderIndex);
        Assert.Equal("Second Choice", result.Value.Choices[1].Text);
        Assert.False(result.Value.Choices[1].IsCorrect);

        // Verify quiz revision was incremented
        Quiz? persistedQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quiz.QuizId, ct));

        Assert.NotNull(persistedQuiz);
        Assert.Equal(2L, persistedQuiz.Revision);

        // Verify question and choices in DB
        Question? persistedQuestion = await harness.ReadDbAsync(async (db, ct) =>
            await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == result.Value.Id, ct));

        Assert.NotNull(persistedQuestion);
        Assert.Equal(host.UserId, persistedQuestion.HostAccountId);
        Assert.Equal(0, persistedQuestion.OrderIndex);

        List<Choice> persistedChoices = await harness.ReadDbAsync(async (db, ct) =>
            await db.Choices.AsNoTracking().Where(c => c.QuestionId == result.Value.Id).OrderBy(c => c.OrderIndex).ToListAsync(ct));

        Assert.Equal(2, persistedChoices.Count);
        Assert.All(persistedChoices, c => Assert.Equal(host.UserId, c.HostAccountId));
    }

    [Fact]
    public async Task AddQuestion_AtTwoHundredQuestions_RejectsWithoutPartialRows()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "MaxQHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz With 200 Qs");

        // Seed 200 questions directly in DB
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            for (int i = 0; i < 200; i++)
            {
                Question q = new()
                {
                    Id = Guid.NewGuid(),
                    QuizId = quiz.QuizId,
                    HostAccountId = host.UserId,
                    OrderIndex = i,
                    Text = $"Seeded Question {i}",
                    DurationSeconds = 20,
                    BasePoints = 1000
                };
                scope.DbContext.Questions.Add(q);
            }
            await scope.DbContext.SaveChangesAsync();
        }

        List<ChoiceRequest> choices = new()
        {
            new("Valid Choice", true),
            new("Another Choice", false)
        };

        AddQuestionCommand command = new(
            quiz.QuizId,
            "Question 201 Should Fail",
            null,
            20,
            1000,
            choices);

        Result<QuestionResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal(QuizErrors.QuestionLimitExceeded.Code, result.Error.Code);

        int totalQuestions = await harness.ReadDbAsync(async (db, ct) =>
            await db.Questions.CountAsync(q => q.QuizId == quiz.QuizId, ct));

        Assert.Equal(200, totalQuestions);

        Quiz? persistedQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quiz.QuizId, ct));

        Assert.NotNull(persistedQuiz);
        Assert.Equal(1L, persistedQuiz.Revision);
    }

    [Fact]
    public async Task UpdateQuestion_ReplacesChoicesAtomically()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "UpdateQHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz for UpdateQ");

        List<ChoiceRequest> initialChoices = new()
        {
            new("Old Choice 1", true),
            new("Old Choice 2", false)
        };

        AddQuestionCommand addCommand = new(
            quiz.QuizId,
            "Initial Question Text",
            null,
            30,
            1000,
            initialChoices);

        Result<QuestionResponse> addResult = await harness.SendAsync(addCommand, TestCaller.Host(host.UserId));
        Assert.True(addResult.IsSuccess);
        Guid questionId = addResult.Value.Id;
        List<Guid> oldChoiceIds = addResult.Value.Choices.Select(c => c.Id).ToList();

        List<ChoiceRequest> newChoices = new()
        {
            new("New Choice A", false),
            new("New Choice B", true),
            new("New Choice C", false)
        };

        UpdateQuestionCommand updateCommand = new(
            quiz.QuizId,
            questionId,
            "Updated Question Text",
            null,
            15,
            2000,
            newChoices);

        Result<QuestionResponse> updateResult = await harness.SendAsync(updateCommand, TestCaller.Host(host.UserId));

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Updated Question Text", updateResult.Value.Text);
        Assert.Equal(15, updateResult.Value.DurationSeconds);
        Assert.Equal(2000, updateResult.Value.BasePoints);
        Assert.Equal(3, updateResult.Value.Choices.Count);

        // Verify old choices deleted and new ones have fresh IDs and contiguous order
        List<Choice> persistedChoices = await harness.ReadDbAsync(async (db, ct) =>
            await db.Choices.AsNoTracking().Where(c => c.QuestionId == questionId).OrderBy(c => c.OrderIndex).ToListAsync(ct));

        Assert.Equal(3, persistedChoices.Count);
        Assert.All(persistedChoices, c => Assert.DoesNotContain(c.Id, oldChoiceIds));
        Assert.Equal(0, persistedChoices[0].OrderIndex);
        Assert.Equal(1, persistedChoices[1].OrderIndex);
        Assert.Equal(2, persistedChoices[2].OrderIndex);

        Quiz? persistedQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quiz.QuizId, ct));

        Assert.NotNull(persistedQuiz);
        Assert.Equal(3L, persistedQuiz.Revision);
    }

    [Fact]
    public async Task QuestionMutation_WithActiveSession_ReturnsQuizInUse()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ActiveSessionHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz In Progress");

        Guid questionId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Question question = new()
            {
                Id = questionId,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Active Question",
                DurationSeconds = 30,
                BasePoints = 1000
            };

            Game game = new()
            {
                Id = Guid.NewGuid(),
                HostAccountId = host.UserId,
                SourceQuizId = quiz.QuizId,
                Title = "Active Game",
                Status = GameStatus.Lobby,
                StateVersion = 1,
                CreatedAt = now
            };

            scope.DbContext.Questions.Add(question);
            scope.DbContext.Games.Add(game);
            await scope.DbContext.SaveChangesAsync();
        }

        List<ChoiceRequest> choices = new()
        {
            new("A", true),
            new("B", false)
        };

        // 1. AddQuestion
        AddQuestionCommand addCommand = new(quiz.QuizId, "New Q", null, 20, 1000, choices);
        Result<QuestionResponse> addResult = await harness.SendAsync(addCommand, TestCaller.Host(host.UserId));
        Assert.False(addResult.IsSuccess);
        Assert.Equal(QuizErrors.InUse.Code, addResult.Error.Code);

        // 2. UpdateQuestion
        UpdateQuestionCommand updateCommand = new(quiz.QuizId, questionId, "Mutated Q", null, 20, 1000, choices);
        Result<QuestionResponse> updateResult = await harness.SendAsync(updateCommand, TestCaller.Host(host.UserId));
        Assert.False(updateResult.IsSuccess);
        Assert.Equal(QuizErrors.InUse.Code, updateResult.Error.Code);

        // 3. DeleteQuestion
        DeleteQuestionCommand deleteCommand = new(quiz.QuizId, questionId);
        Result deleteResult = await harness.SendAsync(deleteCommand, TestCaller.Host(host.UserId));
        Assert.False(deleteResult.IsSuccess);
        Assert.Equal(QuizErrors.InUse.Code, deleteResult.Error.Code);

        // 4. ReorderQuestions
        ReorderQuestionsCommand reorderCommand = new(quiz.QuizId, new List<Guid> { questionId });
        Result<ReorderQuestionsResponse> reorderResult = await harness.SendAsync(reorderCommand, TestCaller.Host(host.UserId));
        Assert.False(reorderResult.IsSuccess);
        Assert.Equal(QuizErrors.InUse.Code, reorderResult.Error.Code);
    }
}
