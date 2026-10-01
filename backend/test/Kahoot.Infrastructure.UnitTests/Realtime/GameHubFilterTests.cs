namespace Kahoot.Infrastructure.UnitTests.Realtime;

using System;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class GameHubFilterTests
{
    private readonly RecordingLogger<GameHubFilter> _logger = new();
    private readonly FilterTestHub _hub = new();
    private readonly MethodInfo _hubMethod;
    private readonly EmptyServiceProvider _serviceProvider = new();

    public GameHubFilterTests()
    {
        _hubMethod = typeof(FilterTestHub).GetMethod(nameof(FilterTestHub.SampleHubMethod))!;
    }

    [Theory]
    [InlineData("SuccessPayload")]
    [InlineData(null)]
    public async Task InvokeMethodAsync_ReturnsDelegateResultWithoutModification(object? expectedResult)
    {
        GameHubFilter filter = new(_logger);
        StubHubCallerContext callerContext = new("conn-1");
        HubInvocationContext invocationContext = CreateInvocationContext(callerContext);

        int executionCount = 0;
        HubInvocationContext? receivedContext = null;

        object? actualResult = await filter.InvokeMethodAsync(invocationContext, context =>
        {
            executionCount++;
            receivedContext = context;
            return ValueTask.FromResult(expectedResult);
        });

        Assert.Equal(1, executionCount);
        Assert.Same(invocationContext, receivedContext);
        Assert.Same(expectedResult, actualResult);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task InvokeMethodAsync_ConvertsUnexpectedExceptionToSafeEnvelope()
    {
        GameHubFilter filter = new(_logger);
        StubHubCallerContext callerContext = new("conn-safe-envelope");
        HubInvocationContext invocationContext = CreateInvocationContext(callerContext);

        const string sensitiveErrorMessage = "Database connection password=SuperSecretPassword123 failed!";
        InvalidOperationException exception = new(sensitiveErrorMessage);

        object? result = await filter.InvokeMethodAsync(invocationContext, _ => throw exception);

        Assert.NotNull(result);
        string json = JsonSerializer.Serialize(result);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);

        JsonElement errorElement = root.GetProperty("error");
        Assert.Equal("Server.InternalError", errorElement.GetProperty("code").GetString());
        Assert.Equal("An unexpected server error occurred.", errorElement.GetProperty("description").GetString());

        Assert.DoesNotContain("SuperSecretPassword123", json);
        Assert.DoesNotContain("StackTrace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeMethodAsync_LogsOriginalExceptionWithInvocationContext()
    {
        GameHubFilter filter = new(_logger);
        const string connectionId = "conn-logging-test-42";
        StubHubCallerContext callerContext = new(connectionId);
        HubInvocationContext invocationContext = CreateInvocationContext(callerContext);

        InvalidOperationException exception = new("Simulated unhandled exception");

        await filter.InvokeMethodAsync(invocationContext, _ => throw exception);

        Assert.Single(_logger.Entries);
        RecordedLogEntry logEntry = _logger.Entries[0];
        Assert.Equal(LogLevel.Error, logEntry.LogLevel);
        Assert.Same(exception, logEntry.Exception);
        Assert.Equal("SampleHubMethod", logEntry.GetValue("Method"));
        Assert.Equal(connectionId, logEntry.GetValue("ConnectionId"));
    }

    [Fact]
    public async Task InvokeMethodAsync_PropagatesCancellation()
    {
        GameHubFilter filter = new(_logger);
        StubHubCallerContext callerContext = new("conn-cancellation");
        HubInvocationContext invocationContext = CreateInvocationContext(callerContext);

        OperationCanceledException operationCanceledException = new("Operation explicitly cancelled");
        OperationCanceledException actualOce = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await filter.InvokeMethodAsync(invocationContext, _ => throw operationCanceledException);
        });

        Assert.Same(operationCanceledException, actualOce);
        Assert.Empty(_logger.Entries);

        TaskCanceledException taskCanceledException = new("Task explicitly cancelled");
        TaskCanceledException actualTce = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await filter.InvokeMethodAsync(invocationContext, _ => throw taskCanceledException);
        });

        Assert.Same(taskCanceledException, actualTce);
        Assert.Empty(_logger.Entries);
    }

    private HubInvocationContext CreateInvocationContext(HubCallerContext callerContext)
    {
        return new HubInvocationContext(callerContext, _serviceProvider, _hub, _hubMethod, Array.Empty<object?>());
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
