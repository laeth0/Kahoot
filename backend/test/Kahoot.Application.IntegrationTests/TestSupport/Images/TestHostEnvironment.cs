namespace Kahoot.Application.IntegrationTests.TestSupport.Images;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class TestHostEnvironment : IHostEnvironment
{
    public TestHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
    }

    public string EnvironmentName { get; set; } = "IntegrationTest";

    public string ApplicationName { get; set; } = "Kahoot.Application.IntegrationTests";

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; }
}
