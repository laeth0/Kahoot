namespace Kahoot.Application.IntegrationTests.Features.Images;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Images;
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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Images")]
[Trait("Phase", "13")]
public sealed class ImageUploadPersistenceTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public ImageUploadPersistenceTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UploadImage_ValidPng_PersistsSanitizedFileAndQuestionImageRow()
    {
        await using TemporaryImageStorage storage = new();
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(
            configureServicesWithConfig: (IServiceCollection services, IConfiguration configuration) =>
            {
                services.AddSingleton<IHostEnvironment>(storage.HostEnvironment);
                services.AddStorage(configuration);
            });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ImageHost1", role: UserRole.Host);

        await using MemoryStream stream = await TestImageGenerator.CreateValidPngStreamAsync(width: 32, height: 48);
        UploadImageCommand command = new(
            Content: stream,
            FileName: "test_banner.png",
            ContentType: "image/png",
            Length: stream.Length);

        Result<UploadImageResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        UploadImageResponse response = result.Value;
        Assert.NotEqual(Guid.Empty, response.ImageId);
        Assert.Equal($"/uploads/{response.ImageId:D}.png", response.Url);

        // Verify physical file existence and dimensions
        Assert.True(storage.FileExists(response.Url));
        string physicalPath = storage.GetPhysicalPath(response.Url);
        FileInfo fileInfo = new(physicalPath);
        Assert.True(fileInfo.Length > 0);

        using (Image image = await Image.LoadAsync(physicalPath))
        {
            Assert.Equal(32, image.Width);
            Assert.Equal(48, image.Height);
        }

        // Verify database persistence
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            QuestionImage? questionImage = await dbContext.QuestionImages.FirstOrDefaultAsync(i => i.Id == response.ImageId);
            Assert.NotNull(questionImage);
            Assert.Equal(host.UserId, questionImage.HostAccountId);
            Assert.Equal(response.Url, questionImage.StoragePath);
            Assert.Equal("image/png", questionImage.ContentType);
            Assert.Equal(32, questionImage.PixelWidth);
            Assert.Equal(48, questionImage.PixelHeight);
            Assert.Equal(fileInfo.Length, questionImage.ByteSize);
            Assert.NotNull(questionImage.UnreferencedSince);
        });
    }

    [Fact]
    public async Task UploadImage_ValidJpegWithExifMetadata_StripsMetadataAndReEncodes()
    {
        await using TemporaryImageStorage storage = new();
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(
            configureServicesWithConfig: (IServiceCollection services, IConfiguration configuration) =>
            {
                services.AddSingleton<IHostEnvironment>(storage.HostEnvironment);
                services.AddStorage(configuration);
            });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ImageHost2", role: UserRole.Host);

        await using MemoryStream stream = await TestImageGenerator.CreateMetadataBearingJpegStreamAsync(
            width: 24,
            height: 24,
            software: "SensitiveExifInformation");

        UploadImageCommand command = new(
            Content: stream,
            FileName: "camera_photo.jpg",
            ContentType: "image/jpeg",
            Length: stream.Length);

        Result<UploadImageResponse> result = await harness.SendAsync(command, TestCaller.Host(host.UserId));

        Assert.True(result.IsSuccess);
        UploadImageResponse response = result.Value;
        Assert.Equal($"/uploads/{response.ImageId:D}.jpg", response.Url);

        string physicalPath = storage.GetPhysicalPath(response.Url);
        Assert.True(File.Exists(physicalPath));

        // Re-read file and assert metadata was stripped
        using (Image loadedImage = await Image.LoadAsync(physicalPath))
        {
            Assert.True(loadedImage.Metadata.ExifProfile is null || loadedImage.Metadata.ExifProfile.Values.Count == 0);
            Assert.Null(loadedImage.Metadata.IptcProfile);
            Assert.Null(loadedImage.Metadata.XmpProfile);
        }
    }

    [Fact]
    public async Task UploadImage_MalformedOrDisallowedPayload_ReturnsErrorAndCleansStaging()
    {
        await using TemporaryImageStorage storage = new();
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(
            configureServicesWithConfig: (IServiceCollection services, IConfiguration configuration) =>
            {
                services.AddSingleton<IHostEnvironment>(storage.HostEnvironment);
                services.AddStorage(configuration);
            });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ImageHost3", role: UserRole.Host);

        // 1. Text payload pretending to be PNG
        byte[] malformedBytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 };
        await using MemoryStream malformedStream = new(malformedBytes);
        UploadImageCommand malformedCmd = new(
            Content: malformedStream,
            FileName: "fake.png",
            ContentType: "image/png",
            Length: malformedStream.Length);

        Result<UploadImageResponse> result = await harness.SendAsync(malformedCmd, TestCaller.Host(host.UserId));

        Assert.True(result.IsFailure);
        Assert.Equal(ImageErrors.InvalidImage.Code, result.Error.Code);

        // Staging directory must be empty
        string[] stagingFiles = Directory.GetFiles(storage.StagingDirectory);
        Assert.Empty(stagingFiles);

        // Uploads directory must be empty
        string[] uploadedFiles = Directory.GetFiles(storage.UploadsDirectory);
        Assert.Empty(uploadedFiles);

        // No database record created
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int count = await dbContext.QuestionImages.CountAsync();
            Assert.Equal(0, count);
        });
    }
}
