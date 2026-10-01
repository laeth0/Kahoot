namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public static class HttpContextFactory
{
    public static DefaultHttpContext Create(
        IPAddress? remoteIpAddress = null,
        string path = "/api/test",
        string traceIdentifier = "test-request-id-12345",
        IServiceProvider? serviceProvider = null)
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.test");
        context.Request.Path = path;
        context.TraceIdentifier = traceIdentifier;
        context.Response.Body = new MemoryStream();

        if (remoteIpAddress is not null)
        {
            context.Connection.RemoteIpAddress = remoteIpAddress;
        }

        if (serviceProvider is not null)
        {
            context.RequestServices = serviceProvider;
        }

        return context;
    }

    public static DefaultHttpContext CreateWithLogging(
        IPAddress? remoteIpAddress = null,
        string path = "/api/test",
        string traceIdentifier = "test-request-id-12345")
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddOptions();
        ServiceProvider provider = services.BuildServiceProvider();

        return Create(remoteIpAddress, path, traceIdentifier, provider);
    }
}
