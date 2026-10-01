namespace Kahoot.Application.IntegrationTests.Features.Composition;

using FluentValidation;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth.Register;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
using Kahoot.Application.Features.Quizzes.UpdateQuiz;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Composition")]
[Trait("Phase", "02")]
public sealed class ValidationPipelineTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public ValidationPipelineTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Send_InvalidRegister_ThrowsValidationExceptionWithoutPersistingUser()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        RegisterCommand invalidCommand = new("ValidUsername", "short");

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            harness.SendAsync(invalidCommand, TestCaller.Anonymous));

        Assert.Contains(exception.Errors, error => error.PropertyName == "Password");

        int userCount = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.CountAsync(ct));
        int tokenCount = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.CountAsync(ct));

        Assert.Equal(0, userCount);
        Assert.Equal(0, tokenCount);
    }

    [Fact]
    public async Task Send_InvalidCreateQuiz_ThrowsBeforePersistence()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "QuizHost");

        CreateQuizCommand invalidCommand = new(string.Empty, "Optional Description");

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            harness.SendAsync(invalidCommand, TestCaller.Host(host.UserId)));

        Assert.Contains(exception.Errors, error => error.PropertyName == "Title");

        int quizCount = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.CountAsync(q => q.HostAccountId == host.UserId, ct));

        Assert.Equal(0, quizCount);
    }

    [Fact]
    public async Task Send_ValidCreateQuiz_PersistsAuditedOwnership()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AuditHost");
        DateTimeOffset beforeSend = DateTimeOffset.UtcNow.AddSeconds(-1);

        CreateQuizCommand command = new("Science Quiz", "A comprehensive science quiz");
        Result<QuizSummaryResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Science Quiz", result.Value.Title);
        Assert.Equal(1L, result.Value.Revision);

        Quiz? persistedQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == result.Value.Id, ct));

        Assert.NotNull(persistedQuiz);
        Assert.Equal(host.UserId, persistedQuiz.HostAccountId);
        Assert.Equal(host.UserId, persistedQuiz.CreatedBy);
        Assert.Equal(host.UserId, persistedQuiz.UpdatedBy);
        Assert.Equal(1L, persistedQuiz.Revision);
        Assert.True(persistedQuiz.CreatedAt >= beforeSend);
        Assert.True(persistedQuiz.UpdatedAt >= beforeSend);
    }

    [Fact]
    public async Task SequentialRequests_UseFreshTrackingState()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "TrackingHost");

        CreateQuizCommand createCommand = new("Initial Quiz Title", "Initial Description");
        Result<QuizSummaryResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid quizId = createResult.Value.Id;

        UpdateQuizCommand updateCommand = new(quizId, "Updated Quiz Title", "Updated Description");
        Result<QuizSummaryResponse> updateResult = await harness.SendAsync(updateCommand, TestCaller.Host(host.UserId));
        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Updated Quiz Title", updateResult.Value.Title);
        Assert.Equal(2L, updateResult.Value.Revision);

        Quiz? readQuiz = await harness.ReadDbAsync(async (db, ct) =>
            await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId, ct));

        Assert.NotNull(readQuiz);
        Assert.Equal("Updated Quiz Title", readQuiz.Title);
        Assert.Equal("Updated Description", readQuiz.Description);
        Assert.Equal(2L, readQuiz.Revision);
    }
}
