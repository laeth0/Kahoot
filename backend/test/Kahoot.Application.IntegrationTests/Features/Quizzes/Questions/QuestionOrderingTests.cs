namespace Kahoot.Application.IntegrationTests.Features.Quizzes.Questions;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;
using Kahoot.Application.Features.Quizzes.ReorderQuestions;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Questions")]
[Trait("Phase", "06")]
public sealed class QuestionOrderingTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public QuestionOrderingTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteQuestion_CompactsMiddleOrdering()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "OrderCompactHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz for Compaction");

        Guid q0Id = Guid.NewGuid();
        Guid q1Id = Guid.NewGuid();
        Guid q2Id = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Question q0 = new()
            {
                Id = q0Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Question 0",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q1 = new()
            {
                Id = q1Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Question 1 (To Delete)",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q2 = new()
            {
                Id = q2Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 2,
                Text = "Question 2",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            scope.DbContext.Questions.Add(q0);
            scope.DbContext.Questions.Add(q1);
            scope.DbContext.Questions.Add(q2);
            await scope.DbContext.SaveChangesAsync();
        }

        DeleteQuestionCommand deleteCommand = new(quiz.QuizId, q1Id);
        Result deleteResult = await harness.SendAsync(deleteCommand, TestCaller.Host(host.UserId));

        Assert.True(deleteResult.IsSuccess);

        List<Question> survivingQuestions = await harness.ReadDbAsync(async (db, ct) =>
            await db.Questions.AsNoTracking().Where(q => q.QuizId == quiz.QuizId).OrderBy(q => q.OrderIndex).ToListAsync(ct));

        Assert.Equal(2, survivingQuestions.Count);
        Assert.Equal(q0Id, survivingQuestions[0].Id);
        Assert.Equal(0, survivingQuestions[0].OrderIndex);

        Assert.Equal(q2Id, survivingQuestions[1].Id);
        Assert.Equal(1, survivingQuestions[1].OrderIndex);

        // Ensure no negative temporary indices exist
        Assert.All(survivingQuestions, q => Assert.True(q.OrderIndex >= 0));

        Quiz? persistedQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quiz.QuizId, ct));

        Assert.NotNull(persistedQuiz);
        Assert.Equal(2L, persistedQuiz.Revision);
    }

    [Fact]
    public async Task ReorderQuestions_ReversesExistingSetWithoutUniqueCollision()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReorderHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz for Reordering");

        Guid q0Id = Guid.NewGuid();
        Guid q1Id = Guid.NewGuid();
        Guid q2Id = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Question q0 = new()
            {
                Id = q0Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Original Q0",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q1 = new()
            {
                Id = q1Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Original Q1",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q2 = new()
            {
                Id = q2Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 2,
                Text = "Original Q2",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            scope.DbContext.Questions.Add(q0);
            scope.DbContext.Questions.Add(q1);
            scope.DbContext.Questions.Add(q2);
            await scope.DbContext.SaveChangesAsync();
        }

        // Reverse the order: [Q2, Q1, Q0]
        List<Guid> reversedIds = new() { q2Id, q1Id, q0Id };
        ReorderQuestionsCommand command = new(quiz.QuizId, reversedIds);
        Result<ReorderQuestionsResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal(2L, result.Value.Revision);

        List<Question> reorderedQuestions = await harness.ReadDbAsync(async (db, ct) =>
            await db.Questions.AsNoTracking().Where(q => q.QuizId == quiz.QuizId).OrderBy(q => q.OrderIndex).ToListAsync(ct));

        Assert.Equal(3, reorderedQuestions.Count);
        Assert.Equal(q2Id, reorderedQuestions[0].Id);
        Assert.Equal(0, reorderedQuestions[0].OrderIndex);

        Assert.Equal(q1Id, reorderedQuestions[1].Id);
        Assert.Equal(1, reorderedQuestions[1].OrderIndex);

        Assert.Equal(q0Id, reorderedQuestions[2].Id);
        Assert.Equal(2, reorderedQuestions[2].OrderIndex);
    }

    [Fact]
    public async Task ReorderQuestions_MissingDuplicateOrForeignIds_DoesNotMutate()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReorderMismatchHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz for Mismatches");

        Guid q0Id = Guid.NewGuid();
        Guid q1Id = Guid.NewGuid();
        Guid q2Id = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Question q0 = new()
            {
                Id = q0Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Q0",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q1 = new()
            {
                Id = q1Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Q1",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q2 = new()
            {
                Id = q2Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 2,
                Text = "Q2",
                DurationSeconds = 20,
                BasePoints = 1000
            };

            scope.DbContext.Questions.Add(q0);
            scope.DbContext.Questions.Add(q1);
            scope.DbContext.Questions.Add(q2);
            await scope.DbContext.SaveChangesAsync();
        }

        // 1. Duplicate ID
        ReorderQuestionsCommand duplicateCommand = new(quiz.QuizId, new List<Guid> { q0Id, q0Id, q1Id });
        Result<ReorderQuestionsResponse> dupResult = await harness.SendAsync(duplicateCommand, TestCaller.Host(host.UserId));
        Assert.False(dupResult.IsSuccess);
        Assert.Equal(QuizErrors.QuestionSetMismatch.Code, dupResult.Error.Code);

        // 2. Missing ID (only 2 out of 3)
        ReorderQuestionsCommand missingCommand = new(quiz.QuizId, new List<Guid> { q0Id, q1Id });
        Result<ReorderQuestionsResponse> missingResult = await harness.SendAsync(missingCommand, TestCaller.Host(host.UserId));
        Assert.False(missingResult.IsSuccess);
        Assert.Equal(QuizErrors.QuestionSetMismatch.Code, missingResult.Error.Code);

        // 3. Foreign ID
        Guid foreignId = Guid.NewGuid();
        ReorderQuestionsCommand foreignCommand = new(quiz.QuizId, new List<Guid> { q0Id, q1Id, foreignId });
        Result<ReorderQuestionsResponse> foreignResult = await harness.SendAsync(foreignCommand, TestCaller.Host(host.UserId));
        Assert.False(foreignResult.IsSuccess);
        Assert.Equal(QuizErrors.QuestionSetMismatch.Code, foreignResult.Error.Code);

        // Verify order and revision remain unchanged
        List<Question> questions = await harness.ReadDbAsync(async (db, ct) =>
            await db.Questions.AsNoTracking().Where(q => q.QuizId == quiz.QuizId).OrderBy(q => q.OrderIndex).ToListAsync(ct));

        Assert.Equal(q0Id, questions[0].Id);
        Assert.Equal(q1Id, questions[1].Id);
        Assert.Equal(q2Id, questions[2].Id);

        Quiz? persistedQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quiz.QuizId, ct));

        Assert.NotNull(persistedQuiz);
        Assert.Equal(1L, persistedQuiz.Revision);
    }
}
