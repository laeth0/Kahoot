namespace Kahoot.Application.IntegrationTests.Features.Images;

using System;
using System.IO;
using System.Threading.Tasks;
using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Images.UploadImage;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Application.IntegrationTests.TestSupport.Images;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Images")]
[Trait("Phase", "13")]
public sealed class ImageUploadCompensationTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public ImageUploadCompensationTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UploadImage_ForeignKeyViolation_CompensatesAndDeletesFile()
    {
        await using TemporaryImageStorage storage = new();
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(
            configureServicesWithConfig: (IServiceCollection services, IConfiguration configuration) =>
            {
                services.AddSingleton<IHostEnvironment>(storage.HostEnvironment);
                services.AddStorage(configuration);
            });

        // Non-existent caller GUID to trigger foreign key violation on users table
        Guid nonExistentHostId = Guid.NewGuid();

        await using MemoryStream stream = await TestImageGenerator.CreateValidPngStreamAsync(width: 16, height: 16);
        UploadImageCommand command = new(
            Content: stream,
            FileName: "orphan_compensation.png",
            ContentType: "image/png",
            Length: stream.Length);

        // Dispatches with non-existent host ID; database foreign key constraint rejects insert
        await Assert.ThrowsAsync<ForeignKeyConstraintViolationException>(async () =>
        {
            await harness.SendAsync(command, TestCaller.Host(nonExistentHostId));
        });

        // Verify compensation: the written final file must have been deleted from the uploads directory
        string[] uploadedFiles = Directory.GetFiles(storage.UploadsDirectory);
        Assert.Empty(uploadedFiles);

        string[] stagingFiles = Directory.GetFiles(storage.StagingDirectory);
        Assert.Empty(stagingFiles);

        // Verify no database record exists
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int imageCount = await dbContext.QuestionImages.CountAsync();
            Assert.Equal(0, imageCount);
        });
    }

    [Fact]
    public async Task UploadImage_UncertainPersistenceFailure_RetainsFileForReconciliation()
    {
        await using TemporaryImageStorage storage = new();
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(
            configureServices: (IServiceCollection services) =>
            {
                // Decorate IAppDbContext to throw an uncertain failure during SaveChangesAsync
                services.AddScoped<IAppDbContext>((IServiceProvider sp) =>
                {
                    AppDbContext inner = sp.GetRequiredService<AppDbContext>();
                    return new FaultInjectionAppDbContext(inner, () =>
                    {
                        throw new InvalidOperationException("Simulated uncertain network/commit failure during SaveChanges.");
                    });
                });
            },
            configureServicesWithConfig: (IServiceCollection services, IConfiguration configuration) =>
            {
                services.AddSingleton<IHostEnvironment>(storage.HostEnvironment);
                services.AddStorage(configuration);
            });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "UncertainHost", role: UserRole.Host);

        await using MemoryStream stream = await TestImageGenerator.CreateValidPngStreamAsync(width: 16, height: 16);
        UploadImageCommand command = new(
            Content: stream,
            FileName: "reconciliation_candidate.png",
            ContentType: "image/png",
            Length: stream.Length);

        // Dispatches command; SaveChangesAsync throws InvalidOperationException (an ambiguous/uncertain exception)
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await harness.SendAsync(command, TestCaller.Host(host.UserId));
        });
        Assert.Contains("Simulated uncertain network/commit failure", thrown.Message);

        // In the uncertain failure branch, the handler retains the file on disk for background reconciliation
        string[] uploadedFiles = Directory.GetFiles(storage.UploadsDirectory);
        Assert.Single(uploadedFiles);
        Assert.True(File.Exists(uploadedFiles[0]));

        // No database row was committed
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int imageCount = await dbContext.QuestionImages.CountAsync();
            Assert.Equal(0, imageCount);
        });
    }
}
