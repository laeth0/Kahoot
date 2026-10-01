namespace Kahoot.Application.IntegrationTests.Features.Quizzes;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
using Kahoot.Application.Features.Quizzes.DeleteQuiz;
using Kahoot.Application.Features.Quizzes.UpdateQuiz;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Quizzes")]
[Trait("Phase", "05")]
public sealed class QuizMutationTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public QuizMutationTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateQuiz_WithNullDescriptionAndTrimmedTitle_PersistsRevision1()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CreateQuizHost");

        CreateQuizCommand command = new("  Trimmed Quiz Title  ", null);
        Result<QuizSummaryResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Trimmed Quiz Title", result.Value.Title);
        Assert.Null(result.Value.Description);
        Assert.Equal(1L, result.Value.Revision);

        Quiz? persistedQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == result.Value.Id, ct));

        Assert.NotNull(persistedQuiz);
        Assert.Equal("Trimmed Quiz Title", persistedQuiz.Title);
        Assert.Null(persistedQuiz.Description);
        Assert.Equal(host.UserId, persistedQuiz.HostAccountId);
        Assert.Equal(host.UserId, persistedQuiz.CreatedBy);
        Assert.Equal(host.UserId, persistedQuiz.UpdatedBy);
        Assert.Equal(1L, persistedQuiz.Revision);
    }

    [Fact]
    public async Task UpdateQuiz_ValidData_UpdatesMetadataAndIncrementsRevision()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "UpdateQuizHost");

        CreateQuizCommand createCommand = new("Initial Title", "Initial Description");
        Result<QuizSummaryResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid quizId = createResult.Value.Id;

        UpdateQuizCommand updateCommand = new(quizId, "Updated Title", "Updated Description");
        Result<QuizSummaryResponse> updateResult = await harness.SendAsync(updateCommand, TestCaller.Host(host.UserId));

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Updated Title", updateResult.Value.Title);
        Assert.Equal("Updated Description", updateResult.Value.Description);
        Assert.Equal(2L, updateResult.Value.Revision);

        Quiz? readQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId, ct));

        Assert.NotNull(readQuiz);
        Assert.Equal("Updated Title", readQuiz.Title);
        Assert.Equal("Updated Description", readQuiz.Description);
        Assert.Equal(2L, readQuiz.Revision);
        Assert.Equal(host.UserId, readQuiz.CreatedBy);
        Assert.Equal(host.UserId, readQuiz.UpdatedBy);
    }

    [Fact]
    public async Task QuizMutation_CrossTenantAccess_ReturnsNotFound()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "HostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "HostB");

        TestQuizRecord quizA = await FeatureData.CreateQuizAsync(harness, hostA.UserId, "Host A Quiz");

        // Host B tries to update Host A's quiz
        UpdateQuizCommand updateCommand = new(quizA.QuizId, "Hacked Title", null);
        Result<QuizSummaryResponse> updateResult = await harness.SendAsync(updateCommand, TestCaller.Host(hostB.UserId));

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(QuizErrors.NotFound.Code, updateResult.Error.Code);

        // Host B tries to delete Host A's quiz
        DeleteQuizCommand deleteCommand = new(quizA.QuizId);
        Result deleteResult = await harness.SendAsync(deleteCommand, TestCaller.Host(hostB.UserId));

        Assert.False(deleteResult.IsSuccess);
        Assert.Equal(QuizErrors.NotFound.Code, deleteResult.Error.Code);

        // Verify Quiz A remains unmodified
        Quiz? readQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizA.QuizId, ct));

        Assert.NotNull(readQuiz);
        Assert.Equal("Host A Quiz", readQuiz.Title);
    }

    [Fact]
    public async Task QuizMutation_AnonymousOrInactive_ReturnsUnauthorized()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord suspendedHost = await FeatureData.CreateUserAsync(
            harness,
            "SuspendedHost",
            status: UserStatus.Suspended);

        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, suspendedHost.UserId, "Some Quiz");

        // 1. Anonymous Create
        CreateQuizCommand createCommand = new("Any Title", null);
        Result<QuizSummaryResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Anonymous);
        Assert.False(createResult.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, createResult.Error.Code);

        // 2. Anonymous Update
        UpdateQuizCommand updateCommand = new(quiz.QuizId, "New Title", null);
        Result<QuizSummaryResponse> updateResult = await harness.SendAsync(updateCommand, TestCaller.Anonymous);
        Assert.False(updateResult.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, updateResult.Error.Code);

        // 3. Anonymous Delete
        DeleteQuizCommand deleteCommand = new(quiz.QuizId);
        Result deleteResult = await harness.SendAsync(deleteCommand, TestCaller.Anonymous);
        Assert.False(deleteResult.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, deleteResult.Error.Code);

        // 4. Inactive/Suspended host Update
        Result<QuizSummaryResponse> suspendedUpdateResult = await harness.SendAsync(updateCommand, TestCaller.Host(suspendedHost.UserId));
        Assert.False(suspendedUpdateResult.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, suspendedUpdateResult.Error.Code);
    }

    [Fact]
    public async Task DeleteQuiz_WithoutSessions_CascadesQuestionsAndChoicesAndOrphansImages()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CascadeHost");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Guid quizId = Guid.NewGuid();
        Guid questionId = Guid.NewGuid();
        Guid imageId = Guid.NewGuid();
        Guid choiceId1 = Guid.NewGuid();
        Guid choiceId2 = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Quiz quiz = new()
            {
                Id = quizId,
                HostAccountId = host.UserId,
                Title = "Quiz To Cascade",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            QuestionImage image = new()
            {
                Id = imageId,
                HostAccountId = host.UserId,
                StoragePath = "uploads/test_image.png",
                ContentType = "image/png",
                ByteSize = 1024,
                PixelWidth = 100,
                PixelHeight = 100,
                CreatedAt = now,
                UnreferencedSince = null
            };

            Question question = new()
            {
                Id = questionId,
                QuizId = quizId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Sample Question",
                ImageId = imageId,
                DurationSeconds = 30,
                BasePoints = 1000
            };

            Choice choice1 = new()
            {
                Id = choiceId1,
                QuestionId = questionId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Choice 1",
                IsCorrect = true
            };

            Choice choice2 = new()
            {
                Id = choiceId2,
                QuestionId = questionId,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Choice 2",
                IsCorrect = false
            };

            scope.DbContext.Quizzes.Add(quiz);
            scope.DbContext.QuestionImages.Add(image);
            scope.DbContext.Questions.Add(question);
            scope.DbContext.Choices.Add(choice1);
            scope.DbContext.Choices.Add(choice2);
            await scope.DbContext.SaveChangesAsync();
        }

        DeleteQuizCommand deleteCommand = new(quizId);
        Result deleteResult = await harness.SendAsync(deleteCommand, TestCaller.Host(host.UserId));

        Assert.True(deleteResult.IsSuccess);

        // Verify quiz, question, and choices are deleted
        Quiz? readQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId, ct));
        Question? readQuestion = await harness.ReadDbAsync(async (db, ct) =>
            await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == questionId, ct));
        int choicesCount = await harness.ReadDbAsync(async (db, ct) =>
            await db.Choices.CountAsync(c => c.QuestionId == questionId, ct));

        Assert.Null(readQuiz);
        Assert.Null(readQuestion);
        Assert.Equal(0, choicesCount);

        // Verify image record remains with UnreferencedSince populated
        QuestionImage? readImage = await harness.ReadDbAsync(async (db, ct) =>
            await db.QuestionImages.AsNoTracking().FirstOrDefaultAsync(img => img.Id == imageId, ct));

        Assert.NotNull(readImage);
        Assert.NotNull(readImage.UnreferencedSince);
    }

    [Fact]
    public async Task DeleteQuiz_WithActiveSession_ReturnsQuizInUse()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ActiveGameHost");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid quizId = Guid.NewGuid();
        Guid gameId = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Quiz quiz = new()
            {
                Id = quizId,
                HostAccountId = host.UserId,
                Title = "Quiz In Active Game",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            Game game = new()
            {
                Id = gameId,
                HostAccountId = host.UserId,
                SourceQuizId = quizId,
                Title = "Quiz In Active Game",
                Status = GameStatus.Lobby,
                StateVersion = 1,
                CreatedAt = now
            };

            scope.DbContext.Quizzes.Add(quiz);
            scope.DbContext.Games.Add(game);
            await scope.DbContext.SaveChangesAsync();
        }

        DeleteQuizCommand deleteCommand = new(quizId);
        Result deleteResult = await harness.SendAsync(deleteCommand, TestCaller.Host(host.UserId));

        Assert.False(deleteResult.IsSuccess);
        Assert.Equal(QuizErrors.InUse.Code, deleteResult.Error.Code);

        Quiz? readQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId, ct));

        Assert.NotNull(readQuiz);
    }

    [Fact]
    public async Task DeleteQuiz_WithFinishedSession_ReturnsQuizHasSessions()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "FinishedGameHost");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid quizId = Guid.NewGuid();
        Guid gameId = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Quiz quiz = new()
            {
                Id = quizId,
                HostAccountId = host.UserId,
                Title = "Historical Quiz",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            Game game = new()
            {
                Id = gameId,
                HostAccountId = host.UserId,
                SourceQuizId = quizId,
                Title = "Historical Quiz",
                Status = GameStatus.Finished,
                StateVersion = 5,
                CreatedAt = now.AddHours(-1),
                FinishedAt = now
            };

            scope.DbContext.Quizzes.Add(quiz);
            scope.DbContext.Games.Add(game);
            await scope.DbContext.SaveChangesAsync();
        }

        DeleteQuizCommand deleteCommand = new(quizId);
        Result deleteResult = await harness.SendAsync(deleteCommand, TestCaller.Host(host.UserId));

        Assert.False(deleteResult.IsSuccess);
        Assert.Equal(QuizErrors.HasSessions.Code, deleteResult.Error.Code);

        Quiz? readQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId, ct));

        Assert.NotNull(readQuiz);
    }
}
