namespace Kahoot.Api.UnitTests.ServiceCollectionExtension;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Kahoot.Api.Middleware;
using Kahoot.Api.ServiceCollectionExtension;
using Kahoot.Api.UnitTests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class JwtBearerEventTests
{
    private readonly JwtBearerEvents _events;
    private readonly JwtBearerOptions _options;
    private readonly AuthenticationScheme _scheme;
    private readonly ServiceProvider _serviceProvider;

    public JwtBearerEventTests()
    {
        IConfiguration configuration = ApiConfiguration.Create();
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddJwtAuthentication(configuration);

        _serviceProvider = services.BuildServiceProvider();
        IOptionsMonitor<JwtBearerOptions> monitor = _serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        _options = monitor.Get(JwtBearerDefaults.AuthenticationScheme);
        _events = (JwtBearerEvents)_options.Events;
        _scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            displayName: "Bearer",
            typeof(JwtBearerHandler));
    }

    [Fact]
    public async Task OnMessageReceived_AtHubPath_ItemsTokenTakesPrecedenceOverQueryToken()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/hubs/game");
        httpContext.Items["access_token"] = "token-from-items-12345";
        httpContext.Request.QueryString = new QueryString("?access_token=token-from-query-67890");

        MessageReceivedContext context = new MessageReceivedContext(httpContext, _scheme, _options);
        await _events.OnMessageReceived(context);

        Assert.Equal("token-from-items-12345", context.Token);
    }

    [Fact]
    public async Task OnMessageReceived_AtHubPath_WithoutItemsToken_ExtractsQueryToken()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/hubs/game");
        httpContext.Request.QueryString = new QueryString("?access_token=token-from-query-67890");

        MessageReceivedContext context = new MessageReceivedContext(httpContext, _scheme, _options);
        await _events.OnMessageReceived(context);

        Assert.Equal("token-from-query-67890", context.Token);
    }

    [Fact]
    public async Task OnMessageReceived_AtHubPath_WithRedactedQueryToken_LeavesTokenUnset()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/hubs/game");
        httpContext.Request.QueryString = new QueryString("?access_token=[REDACTED]");

        MessageReceivedContext context = new MessageReceivedContext(httpContext, _scheme, _options);
        await _events.OnMessageReceived(context);

        Assert.Null(context.Token);
    }

    [Fact]
    public async Task OnMessageReceived_AtHubPath_WithoutAnyToken_LeavesTokenUnset()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/hubs/game");

        MessageReceivedContext context = new MessageReceivedContext(httpContext, _scheme, _options);
        await _events.OnMessageReceived(context);

        Assert.Null(context.Token);
    }

    [Fact]
    public async Task OnMessageReceived_NonHubPath_LeavesTokenUnset()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/api/quizzes");
        httpContext.Items["access_token"] = "token-from-items";
        httpContext.Request.QueryString = new QueryString("?access_token=token-from-query");

        MessageReceivedContext context = new MessageReceivedContext(httpContext, _scheme, _options);
        await _events.OnMessageReceived(context);

        Assert.Null(context.Token);
    }

    [Fact]
    public async Task OnMessageReceived_NonMatchingSegmentPrefix_LeavesTokenUnset()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/hubs/game-other");
        httpContext.Request.QueryString = new QueryString("?access_token=token-from-query");

        MessageReceivedContext context = new MessageReceivedContext(httpContext, _scheme, _options);
        await _events.OnMessageReceived(context);

        Assert.Null(context.Token);
    }

    [Fact]
    public async Task OnMessageReceived_CaseVariantHubPath_ExtractsToken()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/HUBS/GAME");
        httpContext.Request.QueryString = new QueryString("?access_token=token-case-insensitive");

        MessageReceivedContext context = new MessageReceivedContext(httpContext, _scheme, _options);
        await _events.OnMessageReceived(context);

        Assert.Equal("token-case-insensitive", context.Token);
    }

    [Fact]
    public async Task OnMessageReceived_HandoffFromAccessTokenScrubberMiddleware_ExtractsItemsToken()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(path: "/hubs/game");
        httpContext.Request.QueryString = new QueryString("?access_token=pipeline-token-xyz");

        string? capturedToken = null;
        RequestDelegate next = async ctx =>
        {
            MessageReceivedContext msgContext = new MessageReceivedContext(ctx, _scheme, _options);
            await _events.OnMessageReceived(msgContext);
            capturedToken = msgContext.Token;
        };

        AccessTokenScrubberMiddleware middleware = new AccessTokenScrubberMiddleware(next);
        await middleware.InvokeAsync(httpContext);

        Assert.Equal("pipeline-token-xyz", capturedToken);
        Assert.Equal("?access_token=%5BREDACTED%5D", httpContext.Request.QueryString.Value);
    }

    [Fact]
    public async Task OnChallenge_EmitsStandardized401ProblemDetails()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(
            path: "/api/protected",
            traceIdentifier: "test-challenge-req-id",
            serviceProvider: _serviceProvider);

        AuthenticationProperties properties = new AuthenticationProperties();
        JwtBearerChallengeContext context = new JwtBearerChallengeContext(httpContext, _scheme, _options, properties);

        await _events.OnChallenge(context);

        Assert.True(context.Handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        JsonElement root = document.RootElement;

        Assert.Equal(StatusCodes.Status401Unauthorized, root.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", root.GetProperty("title").GetString());
        Assert.Equal("Authentication is required to access this resource, or token is invalid.", root.GetProperty("detail").GetString());
        Assert.Equal("/api/protected", root.GetProperty("instance").GetString());
        Assert.Equal("Auth.Unauthorized", root.GetProperty("code").GetString());
        Assert.Equal("test-challenge-req-id", root.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task OnChallenge_WithAmbientActivity_IncludesW3cTraceId()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(
            path: "/api/protected",
            traceIdentifier: "test-challenge-trace-req",
            serviceProvider: _serviceProvider);

        using Activity activity = new Activity("TestChallengeSpan");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        AuthenticationProperties properties = new AuthenticationProperties();
        JwtBearerChallengeContext context = new JwtBearerChallengeContext(httpContext, _scheme, _options, properties);

        await _events.OnChallenge(context);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        JsonElement root = document.RootElement;

        string? traceIdValue = root.GetProperty("traceId").GetString();
        Assert.NotNull(traceIdValue);
        Assert.Contains(activity.TraceId.ToString(), traceIdValue);
    }

    [Fact]
    public async Task OnForbidden_EmitsStandardized403ProblemDetails()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(
            path: "/api/admin/restricted",
            traceIdentifier: "test-forbidden-req-id",
            serviceProvider: _serviceProvider);

        ForbiddenContext context = new ForbiddenContext(httpContext, _scheme, _options);

        await _events.OnForbidden(context);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        JsonElement root = document.RootElement;

        Assert.Equal(StatusCodes.Status403Forbidden, root.GetProperty("status").GetInt32());
        Assert.Equal("Forbidden", root.GetProperty("title").GetString());
        Assert.Equal("The authenticated account is not allowed to access this resource.", root.GetProperty("detail").GetString());
        Assert.Equal("/api/admin/restricted", root.GetProperty("instance").GetString());
        Assert.Equal("Auth.Forbidden", root.GetProperty("code").GetString());
        Assert.Equal("test-forbidden-req-id", root.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task OnForbidden_WithAmbientActivity_IncludesW3cTraceId()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create(
            path: "/api/admin/restricted",
            traceIdentifier: "test-forbidden-trace-req",
            serviceProvider: _serviceProvider);

        using Activity activity = new Activity("TestForbiddenSpan");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        ForbiddenContext context = new ForbiddenContext(httpContext, _scheme, _options);

        await _events.OnForbidden(context);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        JsonElement root = document.RootElement;

        string? traceIdValue = root.GetProperty("traceId").GetString();
        Assert.NotNull(traceIdValue);
        Assert.Contains(activity.TraceId.ToString(), traceIdValue);
    }

    [Fact]
    public async Task OnTokenValidated_MissingSubjectClaim_FailsBeforeDatabaseResolution()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create();
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim("token_security_version", "1")
        }, "Bearer"));

        TokenValidatedContext context = new TokenValidatedContext(httpContext, _scheme, _options)
        {
            Principal = principal
        };

        await _events.OnTokenValidated(context);

        Assert.True(context.Result?.Failure is not null);
        Assert.Equal("Invalid user identifier in token.", context.Result.Failure.Message);
    }

    [Fact]
    public async Task OnTokenValidated_MalformedSubjectClaim_FailsBeforeDatabaseResolution()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create();
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-valid-guid"),
            new Claim("token_security_version", "1")
        }, "Bearer"));

        TokenValidatedContext context = new TokenValidatedContext(httpContext, _scheme, _options)
        {
            Principal = principal
        };

        await _events.OnTokenValidated(context);

        Assert.True(context.Result?.Failure is not null);
        Assert.Equal("Invalid user identifier in token.", context.Result.Failure.Message);
    }

    [Fact]
    public async Task OnTokenValidated_NameIdentifierTakesPrecedenceOverSub_MalformedPreferredSubjectDoesNotFallBack()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create();
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "malformed-name-identifier"),
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("token_security_version", "1")
        }, "Bearer"));

        TokenValidatedContext context = new TokenValidatedContext(httpContext, _scheme, _options)
        {
            Principal = principal
        };

        await _events.OnTokenValidated(context);

        Assert.True(context.Result?.Failure is not null);
        Assert.Equal("Invalid user identifier in token.", context.Result.Failure.Message);
    }

    [Fact]
    public async Task OnTokenValidated_MissingSecurityVersionClaim_FailsBeforeDatabaseResolution()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create();
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "Bearer"));

        TokenValidatedContext context = new TokenValidatedContext(httpContext, _scheme, _options)
        {
            Principal = principal
        };

        await _events.OnTokenValidated(context);

        Assert.True(context.Result?.Failure is not null);
        Assert.Equal("Missing or invalid token_security_version claim.", context.Result.Failure.Message);
    }

    [Fact]
    public async Task OnTokenValidated_CamelCaseSecurityVersionClaim_DoesNotSatisfyRequirement()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create();
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tokenSecurityVersion", "1")
        }, "Bearer"));

        TokenValidatedContext context = new TokenValidatedContext(httpContext, _scheme, _options)
        {
            Principal = principal
        };

        await _events.OnTokenValidated(context);

        Assert.True(context.Result?.Failure is not null);
        Assert.Equal("Missing or invalid token_security_version claim.", context.Result.Failure.Message);
    }

    [Fact]
    public async Task OnTokenValidated_NonNumericSecurityVersionClaim_FailsBeforeDatabaseResolution()
    {
        DefaultHttpContext httpContext = HttpContextFactory.Create();
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("token_security_version", "invalid_numeric_version")
        }, "Bearer"));

        TokenValidatedContext context = new TokenValidatedContext(httpContext, _scheme, _options)
        {
            Principal = principal
        };

        await _events.OnTokenValidated(context);

        Assert.True(context.Result?.Failure is not null);
        Assert.Equal("Missing or invalid token_security_version claim.", context.Result.Failure.Message);
    }
}
