namespace Kahoot.Application.IntegrationTests.Features.Quizzes.Questions;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.AddQuestion;
using Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Questions")]
[Trait("Phase", "06")]
public sealed class QuestionImageReferenceTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public QuestionImageReferenceTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AttachImage_OwnUnusedImage_ClearsOrphanTimestamp()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ImageAttachHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz for Image");

        Guid imageId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            QuestionImage orphanImage = new()
            {
                Id = imageId,
                HostAccountId = host.UserId,
                StoragePath = "uploads/orphan_image.png",
                ContentType = "image/png",
                ByteSize = 4096,
                PixelWidth = 400,
                PixelHeight = 400,
                CreatedAt = now.AddDays(-2),
                UnreferencedSince = now.AddDays(-1)
            };

            scope.DbContext.QuestionImages.Add(orphanImage);
            await scope.DbContext.SaveChangesAsync();
        }

        List<ChoiceRequest> choices = new()
        {
            new("A", true),
            new("B", false)
        };

        AddQuestionCommand command = new(
            quiz.QuizId,
            "Question with newly attached image",
            imageId,
            25,
            1000,
            choices);

        Result<QuestionResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal("uploads/orphan_image.png", result.Value.ImageUrl);

        QuestionImage? persistedImage = await harness.ReadDbAsync(async (db, ct) =>
            await db.QuestionImages.AsNoTracking().FirstOrDefaultAsync(img => img.Id == imageId, ct));

        Assert.NotNull(persistedImage);
        Assert.Null(persistedImage.UnreferencedSince);
    }

    [Fact]
    public async Task AttachImage_AbsentForeignOrAlreadyAttachedImage_Rejects()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "ImageHostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "ImageHostB");

        TestQuizRecord quizA = await FeatureData.CreateQuizAsync(harness, hostA.UserId, "Quiz A");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Guid foreignImageId = Guid.NewGuid();
        Guid attachedImageId = Guid.NewGuid();
        Guid attachedQuestionId = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostA.UserId)))
        {
            // Foreign image owned by Host B
            QuestionImage foreignImage = new()
            {
                Id = foreignImageId,
                HostAccountId = hostB.UserId,
                StoragePath = "uploads/hostb_image.png",
                ContentType = "image/png",
                ByteSize = 1024,
                PixelWidth = 100,
                PixelHeight = 100,
                CreatedAt = now
            };

            // Image already attached to Question 1 of Quiz A
            QuestionImage attachedImage = new()
            {
                Id = attachedImageId,
                HostAccountId = hostA.UserId,
                StoragePath = "uploads/attached_image.png",
                ContentType = "image/png",
                ByteSize = 1024,
                PixelWidth = 100,
                PixelHeight = 100,
                CreatedAt = now
            };

            Question question1 = new()
            {
                Id = attachedQuestionId,
                QuizId = quizA.QuizId,
                HostAccountId = hostA.UserId,
                OrderIndex = 0,
                Text = "Existing Question",
                ImageId = attachedImageId,
                DurationSeconds = 20,
                BasePoints = 1000
            };

            scope.DbContext.QuestionImages.Add(foreignImage);
            scope.DbContext.QuestionImages.Add(attachedImage);
            scope.DbContext.Questions.Add(question1);
            await scope.DbContext.SaveChangesAsync();
        }

        List<ChoiceRequest> choices = new()
        {
            new("A", true),
            new("B", false)
        };

        // 1. Absent image
        AddQuestionCommand absentCommand = new(quizA.QuizId, "Q Absent", Guid.NewGuid(), 20, 1000, choices);
        Result<QuestionResponse> absentResult = await harness.SendAsync(absentCommand, TestCaller.Host(hostA.UserId));
        Assert.False(absentResult.IsSuccess);
        Assert.Equal(QuizErrors.InvalidImageReference.Code, absentResult.Error.Code);

        // 2. Foreign image
        AddQuestionCommand foreignCommand = new(quizA.QuizId, "Q Foreign", foreignImageId, 20, 1000, choices);
        Result<QuestionResponse> foreignResult = await harness.SendAsync(foreignCommand, TestCaller.Host(hostA.UserId));
        Assert.False(foreignResult.IsSuccess);
        Assert.Equal(QuizErrors.InvalidImageReference.Code, foreignResult.Error.Code);

        // 3. Already attached image
        AddQuestionCommand attachedCommand = new(quizA.QuizId, "Q Already Attached", attachedImageId, 20, 1000, choices);
        Result<QuestionResponse> attachedResult = await harness.SendAsync(attachedCommand, TestCaller.Host(hostA.UserId));
        Assert.False(attachedResult.IsSuccess);
        Assert.Equal(QuizErrors.InvalidImageReference.Code, attachedResult.Error.Code);
    }

    [Fact]
    public async Task ReplaceOrRemoveImage_PreservesSnapshotReferencedImage()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "SnapshotPreserveHost");
        TestQuizRecord quiz = await FeatureData.CreateQuizAsync(harness, host.UserId, "Quiz for Snapshot Preserve");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid imageWithSnapshotId = Guid.NewGuid();
        Guid imageWithoutSnapshotId = Guid.NewGuid();
        Guid question1Id = Guid.NewGuid();
        Guid question2Id = Guid.NewGuid();
        Guid gameId = Guid.NewGuid();

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            QuestionImage imgWithSnapshot = new()
            {
                Id = imageWithSnapshotId,
                HostAccountId = host.UserId,
                StoragePath = "uploads/snapshot_img.png",
                ContentType = "image/png",
                ByteSize = 1024,
                PixelWidth = 100,
                PixelHeight = 100,
                CreatedAt = now
            };

            QuestionImage imgWithoutSnapshot = new()
            {
                Id = imageWithoutSnapshotId,
                HostAccountId = host.UserId,
                StoragePath = "uploads/no_snapshot_img.png",
                ContentType = "image/png",
                ByteSize = 1024,
                PixelWidth = 100,
                PixelHeight = 100,
                CreatedAt = now
            };

            Question q1 = new()
            {
                Id = question1Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 0,
                Text = "Q1",
                ImageId = imageWithSnapshotId,
                DurationSeconds = 20,
                BasePoints = 1000
            };

            Question q2 = new()
            {
                Id = question2Id,
                QuizId = quiz.QuizId,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Q2",
                ImageId = imageWithoutSnapshotId,
                DurationSeconds = 20,
                BasePoints = 1000
            };

            // Finished game referencing imgWithSnapshot via GameQuestionSnapshot
            Game finishedGame = new()
            {
                Id = gameId,
                HostAccountId = host.UserId,
                SourceQuizId = quiz.QuizId,
                Title = "Finished Game",
                Status = GameStatus.Finished,
                StateVersion = 5,
                CreatedAt = now.AddHours(-2),
                FinishedAt = now.AddHours(-1)
            };

            GameQuestionSnapshot snapshot = new()
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                HostAccountId = host.UserId,
                OrderIndex = 1,
                Text = "Snapshot Q",
                ImageId = imageWithSnapshotId,
                DurationSeconds = 20,
                BasePoints = 1000
            };

            scope.DbContext.QuestionImages.Add(imgWithSnapshot);
            scope.DbContext.QuestionImages.Add(imgWithoutSnapshot);
            scope.DbContext.Questions.Add(q1);
            scope.DbContext.Questions.Add(q2);
            scope.DbContext.Games.Add(finishedGame);
            scope.DbContext.GameQuestionSnapshots.Add(snapshot);
            await scope.DbContext.SaveChangesAsync();
        }

        List<ChoiceRequest> choices = new()
        {
            new("A", true),
            new("B", false)
        };

        // 1. Remove image from Q1 (which is referenced by snapshot) -> UnreferencedSince remains null
        UpdateQuestionCommand updateQ1 = new(quiz.QuizId, question1Id, "Q1 Removed Img", null, 20, 1000, choices);
        Result<QuestionResponse> res1 = await harness.SendAsync(updateQ1, TestCaller.Host(host.UserId));
        Assert.True(res1.IsSuccess);

        QuestionImage? readImgWithSnapshot = await harness.ReadDbAsync(async (db, ct) =>
            await db.QuestionImages.AsNoTracking().FirstOrDefaultAsync(img => img.Id == imageWithSnapshotId, ct));

        Assert.NotNull(readImgWithSnapshot);
        Assert.Null(readImgWithSnapshot.UnreferencedSince);

        // 2. Remove image from Q2 (which is NOT referenced by snapshot) -> UnreferencedSince is populated
        UpdateQuestionCommand updateQ2 = new(quiz.QuizId, question2Id, "Q2 Removed Img", null, 20, 1000, choices);
        Result<QuestionResponse> res2 = await harness.SendAsync(updateQ2, TestCaller.Host(host.UserId));
        Assert.True(res2.IsSuccess);

        QuestionImage? readImgWithoutSnapshot = await harness.ReadDbAsync(async (db, ct) =>
            await db.QuestionImages.AsNoTracking().FirstOrDefaultAsync(img => img.Id == imageWithoutSnapshotId, ct));

        Assert.NotNull(readImgWithoutSnapshot);
        Assert.NotNull(readImgWithoutSnapshot.UnreferencedSince);
    }
}
