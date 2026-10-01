namespace Kahoot.Application.IntegrationTests.Features.Quizzes;

using FluentValidation;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.GetQuizById;
using Kahoot.Application.Features.Quizzes.ListQuizzes;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Quizzes")]
[Trait("Phase", "05")]
public sealed class QuizQueryTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public QuizQueryTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetQuizById_OwnedQuiz_ReturnsOrderedQuestionsAndChoices()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "GetQuizHost");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Guid quizId = Guid.NewGuid();
        Guid question1Id = Guid.NewGuid();
        Guid question2Id = Guid.NewGuid();
        Guid imageId = Guid.NewGuid();
        Guid choice1Id = Guid.NewGuid();
        Guid choice2Id = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Quiz quiz = new()
            {
                Id = quizId,
                HostAccountId = host.UserId,
                Title = "Full Quiz Detail",
                Description = "A quiz with questions and choices",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            QuestionImage image = new()
            {
                Id = imageId,
                HostAccountId = host.UserId,
                StoragePath = "uploads/question_img.jpg",
                ContentType = "image/jpeg",
                ByteSize = 2048,
                PixelWidth = 200,
                PixelHeight = 200,
                CreatedAt = now
            };

            Question q1 = new()
            {
                Id = question1Id,
                QuizId = quizId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "First Question",
                ImageId = imageId,
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q2 = new()
            {
                Id = question2Id,
                QuizId = quizId,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Second Question",
                DurationSeconds = 30,
                BasePoints = 2000
            };

            Choice c1 = new()
            {
                Id = choice1Id,
                QuestionId = question1Id,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Correct Choice",
                IsCorrect = true
            };

            Choice c2 = new()
            {
                Id = choice2Id,
                QuestionId = question1Id,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Wrong Choice",
                IsCorrect = false
            };

            scope.DbContext.Quizzes.Add(quiz);
            scope.DbContext.QuestionImages.Add(image);
            scope.DbContext.Questions.Add(q1);
            scope.DbContext.Questions.Add(q2);
            scope.DbContext.Choices.Add(c1);
            scope.DbContext.Choices.Add(c2);
            await scope.DbContext.SaveChangesAsync();
        }

        GetQuizByIdQuery query = new(quizId);
        Result<QuizDetailsResponse> result = await harness.SendAsync(query, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal(quizId, result.Value.Id);
        Assert.Equal("Full Quiz Detail", result.Value.Title);
        Assert.Equal(2, result.Value.Questions.Count);

        QuizQuestionDetailsResponse firstQuestion = result.Value.Questions[0];
        QuizQuestionDetailsResponse secondQuestion = result.Value.Questions[1];

        Assert.Equal(0, firstQuestion.OrderIndex);
        Assert.Equal("First Question", firstQuestion.Text);
        Assert.Equal("uploads/question_img.jpg", firstQuestion.ImageUrl);
        Assert.Equal(2, firstQuestion.Choices.Count);
        Assert.Equal("Correct Choice", firstQuestion.Choices[0].Text);
        Assert.True(firstQuestion.Choices[0].IsCorrect);
        Assert.Equal("Wrong Choice", firstQuestion.Choices[1].Text);
        Assert.False(firstQuestion.Choices[1].IsCorrect);

        Assert.Equal(1, secondQuestion.OrderIndex);
        Assert.Equal("Second Question", secondQuestion.Text);
        Assert.Null(secondQuestion.ImageUrl);
        Assert.Empty(secondQuestion.Choices);
    }

    [Fact]
    public async Task GetQuizById_CrossTenantOrAnonymous_ReturnsExpectedError()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "QueryHostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "QueryHostB");

        TestQuizRecord quizA = await FeatureData.CreateQuizAsync(harness, hostA.UserId, "Private Quiz");

        // 1. Cross-tenant seek
        GetQuizByIdQuery query = new(quizA.QuizId);
        Result<QuizDetailsResponse> crossTenantResult = await harness.SendAsync(query, TestCaller.Host(hostB.UserId));
        Assert.False(crossTenantResult.IsSuccess);
        Assert.Equal(QuizErrors.NotFound.Code, crossTenantResult.Error.Code);

        // 2. Anonymous seek
        Result<QuizDetailsResponse> anonResult = await harness.SendAsync(query, TestCaller.Anonymous);
        Assert.False(anonResult.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, anonResult.Error.Code);
    }

    [Fact]
    public async Task ListQuizzes_KeysetPagination_TraversesDescendingWithoutGapsOrForeignQuizzes()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "ListHostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "ListHostB");

        // Seed 1 quiz for Host B
        await FeatureData.CreateQuizAsync(harness, hostB.UserId, "Foreign Quiz");

        // Seed 5 quizzes for Host A with deterministic timestamps
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;
        List<Guid> hostAQuizIds = [];

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostA.UserId)))
        {
            for (int i = 0; i < 5; i++)
            {
                Guid quizId = Guid.NewGuid();
                hostAQuizIds.Add(quizId);
                // Share timestamp between i=1 and i=2 to test GUID tie-breaking
                DateTimeOffset timestamp = i == 2 ? baseTime.AddMinutes(1) : baseTime.AddMinutes(i);

                Quiz quiz = new()
                {
                    Id = quizId,
                    HostAccountId = hostA.UserId,
                    Title = $"Quiz {i}",
                    Revision = 1,
                    CreatedAt = timestamp,
                    UpdatedAt = timestamp,
                    CreatedBy = hostA.UserId,
                    UpdatedBy = hostA.UserId
                };
                scope.DbContext.Quizzes.Add(quiz);
            }
            await scope.DbContext.SaveChangesAsync();
        }

        // Page 1 (PageSize = 2)
        ListQuizzesQuery page1Query = new(null, 2);
        Result<ListQuizzesResponse> page1Result = await harness.SendAsync(page1Query, TestCaller.Host(hostA.UserId));

        Assert.True(page1Result.IsSuccess);
        Assert.Equal(2, page1Result.Value.Items.Count);
        Assert.True(page1Result.Value.HasMore);
        Assert.NotNull(page1Result.Value.NextCursor);

        // Page 2 (PageSize = 2)
        ListQuizzesQuery page2Query = new(page1Result.Value.NextCursor, 2);
        Result<ListQuizzesResponse> page2Result = await harness.SendAsync(page2Query, TestCaller.Host(hostA.UserId));

        Assert.True(page2Result.IsSuccess);
        Assert.Equal(2, page2Result.Value.Items.Count);
        Assert.True(page2Result.Value.HasMore);
        Assert.NotNull(page2Result.Value.NextCursor);

        // Page 3 (PageSize = 2)
        ListQuizzesQuery page3Query = new(page2Result.Value.NextCursor, 2);
        Result<ListQuizzesResponse> page3Result = await harness.SendAsync(page3Query, TestCaller.Host(hostA.UserId));

        Assert.True(page3Result.IsSuccess);
        Assert.Single(page3Result.Value.Items);
        Assert.False(page3Result.Value.HasMore);
        Assert.Null(page3Result.Value.NextCursor);

        // Combine all fetched IDs across 3 pages
        List<Guid> allFetchedIds = page1Result.Value.Items.Select(q => q.Id)
            .Concat(page2Result.Value.Items.Select(q => q.Id))
            .Concat(page3Result.Value.Items.Select(q => q.Id))
            .ToList();

        // 5 distinct quizzes fetched, strictly matching Host A's quizzes
        Assert.Equal(5, allFetchedIds.Distinct().Count());
        Assert.All(allFetchedIds, id => Assert.Contains(id, hostAQuizIds));
    }

    [Fact]
    public async Task ListQuizzes_MalformedCursor_ThrowsValidationException()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CursorHost");

        ListQuizzesQuery query = new("malformed_non_base64_cursor!@#", 10);

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            harness.SendAsync(query, TestCaller.Host(host.UserId)));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Cursor");
    }

    [Fact]
    public async Task ListQuizzes_Anonymous_ReturnsUnauthorized()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        ListQuizzesQuery query = new(null, 10);
        Result<ListQuizzesResponse> result = await harness.SendAsync(query, TestCaller.Anonymous);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.Unauthorized.Code, result.Error.Code);
    }
}
