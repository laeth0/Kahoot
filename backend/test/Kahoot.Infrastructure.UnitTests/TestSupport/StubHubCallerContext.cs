namespace Kahoot.Infrastructure.UnitTests.TestSupport;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

public sealed class StubHubCallerContext : HubCallerContext
{
    private readonly string _connectionId;
    private readonly ClaimsPrincipal? _user;
    private readonly IDictionary<object, object?> _items = new Dictionary<object, object?>();
    private readonly IFeatureCollection _features = new FeatureCollection();
    private readonly CancellationTokenSource _cts = new();

    public StubHubCallerContext(string connectionId = "conn-stub-1", ClaimsPrincipal? user = null)
    {
        _connectionId = connectionId;
        _user = user;
    }

    public override string ConnectionId => _connectionId;

    public override string? UserIdentifier => _user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public override ClaimsPrincipal? User => _user;

    public override IDictionary<object, object?> Items => _items;

    public override IFeatureCollection Features => _features;

    public override CancellationToken ConnectionAborted => _cts.Token;

    public override void Abort()
    {
        _cts.Cancel();
    }
}
