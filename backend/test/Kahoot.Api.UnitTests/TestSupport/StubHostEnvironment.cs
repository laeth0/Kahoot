namespace Kahoot.Api.UnitTests.TestSupport;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class StubHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";

    public string ApplicationName { get; set; } = "Kahoot.Api";

    public string ContentRootPath { get; set; } = "C:\\test\\app";

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
