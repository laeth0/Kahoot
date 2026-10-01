namespace Kahoot.Api.UnitTests.Middleware;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kahoot.Api.Middleware;
using Kahoot.Api.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Xunit;

public sealed class AccessTokenScrubberMiddlewareTests
{
    [Theory]
    [InlineData("/hubs/game")]
    [InlineData("/hubs/game/negotiate")]
    public async Task InvokeAsync_RedactsHubTokenAndPreservesOriginalInItems(string hubPath)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: hubPath);
        const string secretToken = "secret-player-jwt-token-value";
        context.Request.QueryString = new QueryString($"?access_token={secretToken}&id=42");

        int nextCallCount = 0;
        string? querySeenByNext = null;
        RequestDelegate next = ctx =>
        {
            nextCallCount++;
            querySeenByNext = ctx.Request.QueryString.Value;
            return Task.CompletedTask;
        };

        AccessTokenScrubberMiddleware middleware = new(next);
        await middleware.InvokeAsync(context);

        Assert.Equal(1, nextCallCount);
        Assert.NotNull(querySeenByNext);
        Assert.DoesNotContain(secretToken, querySeenByNext);
        Assert.Contains("access_token=%5BREDACTED%5D", querySeenByNext, StringComparison.OrdinalIgnoreCase);

        Dictionary<string, StringValues> parsedNextQuery = QueryHelpers.ParseQuery(querySeenByNext);
        Assert.Equal("[REDACTED]", parsedNextQuery["access_token"].ToString());
        Assert.Equal("42", parsedNextQuery["id"].ToString());

        Assert.True(context.Items.TryGetValue("access_token", out object? itemsToken));
        Assert.Equal(secretToken, itemsToken);
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/hubs/game-other")]
    [InlineData("/api/quizzes")]
    public async Task InvokeAsync_RedactsNonHubTokenWithoutRetainingCredential(string nonHubPath)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: nonHubPath);
        const string secretToken = "non-hub-secret-token";
        context.Request.QueryString = new QueryString($"?access_token={secretToken}");

        int nextCallCount = 0;
        RequestDelegate next = ctx =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        };

        AccessTokenScrubberMiddleware middleware = new(next);
        await middleware.InvokeAsync(context);

        Assert.Equal(1, nextCallCount);
        string queryString = context.Request.QueryString.Value ?? string.Empty;
        Assert.DoesNotContain(secretToken, queryString);

        Dictionary<string, StringValues> parsedQuery = QueryHelpers.ParseQuery(queryString);
        Assert.Equal("[REDACTED]", parsedQuery["access_token"].ToString());

        Assert.False(context.Items.ContainsKey("access_token"));
    }

    [Fact]
    public async Task InvokeAsync_PreservesUnrelatedParametersAndRepeatedValues()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/hubs/game");
        const string secretToken = "my-secret-token";
        context.Request.QueryString = new QueryString($"?category=math&tag=geometry&tag=algebra&encoded=hello%20world&access_token={secretToken}");

        RequestDelegate next = ctx => Task.CompletedTask;
        AccessTokenScrubberMiddleware middleware = new(next);

        await middleware.InvokeAsync(context);

        Dictionary<string, StringValues> parsedQuery = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
        Assert.Equal("math", parsedQuery["category"].ToString());

        StringValues tags = parsedQuery["tag"];
        Assert.Equal(2, tags.Count);
        Assert.Equal("geometry", tags[0]);
        Assert.Equal("algebra", tags[1]);

        Assert.Equal("hello world", parsedQuery["encoded"].ToString());
        Assert.Equal("[REDACTED]", parsedQuery["access_token"].ToString());
        Assert.Equal(secretToken, context.Items["access_token"]);
    }

    [Fact]
    public async Task InvokeAsync_RedactsAllRepeatedTokenValues()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/hubs/game");
        context.Request.QueryString = new QueryString("?access_token=token-part-1&access_token=token-part-2");

        RequestDelegate next = ctx => Task.CompletedTask;
        AccessTokenScrubberMiddleware middleware = new(next);

        await middleware.InvokeAsync(context);

        Dictionary<string, StringValues> parsedQuery = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
        StringValues redactedTokens = parsedQuery["access_token"];
        Assert.Equal(2, redactedTokens.Count);
        Assert.Equal("[REDACTED]", redactedTokens[0]);
        Assert.Equal("[REDACTED]", redactedTokens[1]);

        Assert.True(context.Items.TryGetValue("access_token", out object? itemsToken));
        Assert.Equal("token-part-1,token-part-2", itemsToken);
    }

    [Fact]
    public async Task InvokeAsync_HandlesKeyAndHubPathCaseAccordingToSource()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/HUBS/GAME/NEGOTIATE");
        const string secretToken = "cased-token-value";
        context.Request.QueryString = new QueryString($"?ACCESS_TOKEN={secretToken}");

        RequestDelegate next = ctx => Task.CompletedTask;
        AccessTokenScrubberMiddleware middleware = new(next);

        await middleware.InvokeAsync(context);

        Dictionary<string, StringValues> parsedQuery = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
        Assert.True(parsedQuery.ContainsKey("ACCESS_TOKEN") || parsedQuery.ContainsKey("access_token"));

        string queryString = context.Request.QueryString.Value ?? string.Empty;
        Assert.DoesNotContain(secretToken, queryString);

        Assert.True(context.Items.TryGetValue("access_token", out object? itemsToken));
        Assert.Equal(secretToken, itemsToken);
    }

    [Fact]
    public async Task InvokeAsync_LeavesAbsentOrEmptyTokenUnchanged()
    {
        DefaultHttpContext contextMissing = HttpContextFactory.Create(path: "/hubs/game");
        contextMissing.Request.QueryString = new QueryString("?filter=active");

        int nextCount1 = 0;
        RequestDelegate next1 = ctx =>
        {
            nextCount1++;
            return Task.CompletedTask;
        };

        AccessTokenScrubberMiddleware middleware1 = new(next1);
        await middleware1.InvokeAsync(contextMissing);

        Assert.Equal(1, nextCount1);
        Assert.Equal("?filter=active", contextMissing.Request.QueryString.Value);
        Assert.False(contextMissing.Items.ContainsKey("access_token"));

        DefaultHttpContext contextEmpty = HttpContextFactory.Create(path: "/hubs/game");
        contextEmpty.Request.QueryString = new QueryString("?access_token=&filter=active");

        int nextCount2 = 0;
        RequestDelegate next2 = ctx =>
        {
            nextCount2++;
            return Task.CompletedTask;
        };

        AccessTokenScrubberMiddleware middleware2 = new(next2);
        await middleware2.InvokeAsync(contextEmpty);

        Assert.Equal(1, nextCount2);
        Assert.False(contextEmpty.Items.ContainsKey("access_token"));

        DefaultHttpContext contextWhitespace = HttpContextFactory.Create(path: "/hubs/game");
        contextWhitespace.Request.QueryString = new QueryString("?access_token=%20%20%20");

        int nextCount3 = 0;
        RequestDelegate next3 = ctx =>
        {
            nextCount3++;
            return Task.CompletedTask;
        };

        AccessTokenScrubberMiddleware middleware3 = new(next3);
        await middleware3.InvokeAsync(contextWhitespace);

        Assert.Equal(1, nextCount3);
        Dictionary<string, StringValues> parsedWhitespace = QueryHelpers.ParseQuery(contextWhitespace.Request.QueryString.Value);
        Assert.Equal("[REDACTED]", parsedWhitespace["access_token"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_PropagatesDownstreamException()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/hubs/game");
        const string secretToken = "secret-token-to-scrub";
        context.Request.QueryString = new QueryString($"?access_token={secretToken}");

        InvalidOperationException expectedException = new("Downstream pipeline faulted.");
        RequestDelegate next = ctx => throw expectedException;

        AccessTokenScrubberMiddleware middleware = new(next);
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Same(expectedException, thrown);

        string queryString = context.Request.QueryString.Value ?? string.Empty;
        Assert.DoesNotContain(secretToken, queryString);
        Assert.True(context.Items.ContainsKey("access_token"));
    }
}
