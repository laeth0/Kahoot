namespace Kahoot.Infrastructure.UnitTests.Persistence;

using System;
using System.Threading.Tasks;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Domain.Entities;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Xunit;

public sealed class AuditableEntityInterceptorTests
{
    private readonly DateTimeOffset _initialTime = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly Guid _callerUserId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a010");

    [Fact]
    public void SaveChanges_AddedEntityGetsInjectedUtcTimestampsAndActor_Sync()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(_callerUserId);
        using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        Quiz quiz = CreateSampleQuiz();
        context.Quizzes.Add(quiz);

        int result = context.SaveChanges();

        Assert.Equal(0, result);
        Assert.Equal(_initialTime, quiz.CreatedAt);
        Assert.Equal(_initialTime, quiz.UpdatedAt);
        Assert.Equal(_callerUserId, quiz.CreatedBy);
        Assert.Equal(_callerUserId, quiz.UpdatedBy);
    }

    [Fact]
    public async Task SaveChanges_AddedEntityGetsInjectedUtcTimestampsAndActor_Async()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(_callerUserId);
        await using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        Quiz quiz = CreateSampleQuiz();
        context.Quizzes.Add(quiz);

        int result = await context.SaveChangesAsync();

        Assert.Equal(0, result);
        Assert.Equal(_initialTime, quiz.CreatedAt);
        Assert.Equal(_initialTime, quiz.UpdatedAt);
        Assert.Equal(_callerUserId, quiz.CreatedBy);
        Assert.Equal(_callerUserId, quiz.UpdatedBy);
    }

    [Fact]
    public void SaveChanges_AddedEntityPreservesExplicitActors()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(_callerUserId);
        using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        Guid explicitCreator = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a020");
        Quiz quizWithExplicitCreatorOnly = CreateSampleQuiz();
        quizWithExplicitCreatorOnly.CreatedBy = explicitCreator;

        Guid explicitUpdater = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a021");
        Quiz quizWithBothExplicit = CreateSampleQuiz();
        quizWithBothExplicit.CreatedBy = explicitCreator;
        quizWithBothExplicit.UpdatedBy = explicitUpdater;

        context.Quizzes.AddRange(quizWithExplicitCreatorOnly, quizWithBothExplicit);
        context.SaveChanges();

        Assert.Equal(explicitCreator, quizWithExplicitCreatorOnly.CreatedBy);
        Assert.Equal(_callerUserId, quizWithExplicitCreatorOnly.UpdatedBy);
        Assert.Equal(_initialTime, quizWithExplicitCreatorOnly.CreatedAt);
        Assert.Equal(_initialTime, quizWithExplicitCreatorOnly.UpdatedAt);

        Assert.Equal(explicitCreator, quizWithBothExplicit.CreatedBy);
        Assert.Equal(explicitUpdater, quizWithBothExplicit.UpdatedBy);
        Assert.Equal(_initialTime, quizWithBothExplicit.CreatedAt);
        Assert.Equal(_initialTime, quizWithBothExplicit.UpdatedAt);
    }

    [Fact]
    public void SaveChanges_AddedEntityWithoutCallerKeepsNullActors()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(userId: null);
        using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        Quiz quiz = CreateSampleQuiz();
        context.Quizzes.Add(quiz);
        context.SaveChanges();

        Assert.Null(quiz.CreatedBy);
        Assert.Null(quiz.UpdatedBy);
        Assert.Equal(_initialTime, quiz.CreatedAt);
        Assert.Equal(_initialTime, quiz.UpdatedAt);
    }

    [Fact]
    public async Task SaveChanges_ModifiedEntityUpdatesOnlyMutableAuditMetadata()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(_callerUserId);
        await using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        DateTimeOffset originalCreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Guid originalCreator = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a030");

        Quiz quiz = CreateSampleQuiz();
        quiz.CreatedAt = originalCreatedAt;
        quiz.UpdatedAt = originalCreatedAt;
        quiz.CreatedBy = originalCreator;
        quiz.UpdatedBy = originalCreator;

        EntityEntry<Quiz> entry = context.Quizzes.Attach(quiz);
        entry.State = EntityState.Modified;

        // Explicitly attempt to tamper with immutable audit fields
        entry.Property(q => q.CreatedAt).IsModified = true;
        entry.Property(q => q.CreatedBy).IsModified = true;

        DateTimeOffset modifiedTime = _initialTime.AddHours(2);
        timeProvider.SetUtcNow(modifiedTime);

        int result = await context.SaveChangesAsync();

        Assert.Equal(0, result);
        Assert.Equal(originalCreatedAt, quiz.CreatedAt);
        Assert.Equal(originalCreator, quiz.CreatedBy);
        Assert.Equal(modifiedTime, quiz.UpdatedAt);
        Assert.Equal(_callerUserId, quiz.UpdatedBy);

        Assert.False(entry.Property(q => q.CreatedAt).IsModified);
        Assert.False(entry.Property(q => q.CreatedBy).IsModified);
    }

    [Fact]
    public void SaveChanges_ModifiedEntityWithoutCallerPreservesUpdatedBy()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(userId: null);
        using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        DateTimeOffset originalTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Guid originalUpdater = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a040");

        Quiz quiz = CreateSampleQuiz();
        quiz.CreatedAt = originalTime;
        quiz.UpdatedAt = originalTime;
        quiz.CreatedBy = originalUpdater;
        quiz.UpdatedBy = originalUpdater;

        EntityEntry<Quiz> entry = context.Quizzes.Attach(quiz);
        entry.State = EntityState.Modified;

        DateTimeOffset modifiedTime = _initialTime.AddHours(1);
        timeProvider.SetUtcNow(modifiedTime);

        context.SaveChanges();

        Assert.Equal(originalUpdater, quiz.UpdatedBy);
        Assert.Equal(modifiedTime, quiz.UpdatedAt);
    }

    [Fact]
    public void SaveChanges_UnchangedAndDeletedEntitiesAreNotStamped()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(_callerUserId);
        using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        DateTimeOffset originalTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Guid originalActor = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a050");

        Quiz unchangedQuiz = CreateSampleQuiz();
        unchangedQuiz.CreatedAt = originalTime;
        unchangedQuiz.UpdatedAt = originalTime;
        unchangedQuiz.CreatedBy = originalActor;
        unchangedQuiz.UpdatedBy = originalActor;

        Quiz deletedQuiz = CreateSampleQuiz();
        deletedQuiz.CreatedAt = originalTime;
        deletedQuiz.UpdatedAt = originalTime;
        deletedQuiz.CreatedBy = originalActor;
        deletedQuiz.UpdatedBy = originalActor;

        EntityEntry<Quiz> unchangedEntry = context.Quizzes.Attach(unchangedQuiz);
        unchangedEntry.State = EntityState.Unchanged;

        EntityEntry<Quiz> deletedEntry = context.Quizzes.Attach(deletedQuiz);
        deletedEntry.State = EntityState.Deleted;

        timeProvider.Advance(TimeSpan.FromHours(5));
        context.SaveChanges();

        Assert.Equal(originalTime, unchangedQuiz.CreatedAt);
        Assert.Equal(originalTime, unchangedQuiz.UpdatedAt);
        Assert.Equal(originalActor, unchangedQuiz.CreatedBy);
        Assert.Equal(originalActor, unchangedQuiz.UpdatedBy);

        Assert.Equal(originalTime, deletedQuiz.CreatedAt);
        Assert.Equal(originalTime, deletedQuiz.UpdatedAt);
        Assert.Equal(originalActor, deletedQuiz.CreatedBy);
        Assert.Equal(originalActor, deletedQuiz.UpdatedBy);
    }

    [Fact]
    public void SaveChanges_AllAddedEntriesUseSameTimestamp()
    {
        ManualTimeProvider timeProvider = new(_initialTime);
        StubCurrentUser currentUser = new(_callerUserId);
        using AppDbContext context = CreateDbContext(timeProvider, currentUser);

        Quiz quiz1 = CreateSampleQuiz();
        Quiz quiz2 = CreateSampleQuiz();

        context.Quizzes.AddRange(quiz1, quiz2);
        context.SaveChanges();

        Assert.Equal(_initialTime, quiz1.CreatedAt);
        Assert.Equal(_initialTime, quiz1.UpdatedAt);
        Assert.Equal(_initialTime, quiz2.CreatedAt);
        Assert.Equal(_initialTime, quiz2.UpdatedAt);
    }

    private static AppDbContext CreateDbContext(TimeProvider timeProvider, ICurrentUser currentUser)
    {
        DbContextOptionsBuilder<AppDbContext> builder = new();
        builder.UseNpgsql("Host=127.0.0.1;Database=unit_test_model;Username=unit_test");
        builder.AddInterceptors(
            new AuditableEntityInterceptor(timeProvider, currentUser),
            new SuppressPersistenceInterceptor());

        return new AppDbContext(builder.Options);
    }

    private static Quiz CreateSampleQuiz()
    {
        return new Quiz
        {
            Id = Guid.NewGuid(),
            HostAccountId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a099"),
            Title = "General Knowledge Quiz"
        };
    }
}
