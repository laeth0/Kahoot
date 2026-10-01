namespace Kahoot.Infrastructure.UnitTests.TestSupport;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

public sealed class RecordingHubContext : IHubContext<GameHub>
{
    private readonly ConcurrentQueue<RecordedSend> _sends = new();
    private readonly RecordingHubClients _clients;

    public IReadOnlyList<RecordedSend> Sends => _sends.ToArray();

    public Func<RecordedSend, Task>? SendHandler { get; set; }

    public RecordingHubContext()
    {
        _clients = new RecordingHubClients(this);
    }

    public IHubClients Clients => _clients;

    public IGroupManager Groups => throw new NotSupportedException("GroupManager is not supported or expected in GameNotificationService.");

    internal async Task RecordAndExecuteAsync(string group, string method, object?[] args, CancellationToken cancellationToken)
    {
        RecordedSend send = new(group, method, args, cancellationToken);
        _sends.Enqueue(send);

        if (SendHandler is not null)
        {
            await SendHandler(send);
        }
    }

    private sealed class RecordingHubClients : IHubClients
    {
        private readonly RecordingHubContext _parent;

        public RecordingHubClients(RecordingHubContext parent)
        {
            _parent = parent;
        }

        public IClientProxy Group(string groupName) => new RecordingClientProxy(_parent, groupName);

        public IClientProxy All => throw new NotSupportedException("Broadcasting to All is not permitted.");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        private readonly RecordingHubContext _parent;
        private readonly string _group;

        public RecordingClientProxy(RecordingHubContext parent, string group)
        {
            _parent = parent;
            _group = group;
        }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return _parent.RecordAndExecuteAsync(_group, method, args, cancellationToken);
        }
    }
}
