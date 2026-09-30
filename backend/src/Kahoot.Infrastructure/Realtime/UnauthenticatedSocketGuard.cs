namespace Kahoot.Infrastructure.Realtime;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

// Unauthenticated Socket Guard (RT-FAIL-001, RT-RISK-004, RT-TEST-009) - Enforces 15-second handshake abandonment timeout on new WebSocket connections to eliminate idle zombie sockets.
public sealed class UnauthenticatedSocketGuard
{
    // Handshake Timeout Threshold (RT-FAIL-001) - Clamps unauthenticated connection lifetime strictly to 15 seconds.
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<string, SocketTimeoutRegistration> _registrations = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UnauthenticatedSocketGuard> _logger;

    public UnauthenticatedSocketGuard(
        TimeProvider timeProvider,
        ILogger<UnauthenticatedSocketGuard> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Handshake Timeout Registration - Initiates non-blocking 15-second timer for newly accepted SignalR connection.
    public void Track(string connectionId, Action abortConnection)
    {
        ITimer timer = _timeProvider.CreateTimer(
            OnTimeout,
            connectionId,
            HandshakeTimeout,
            Timeout.InfiniteTimeSpan);

        SocketTimeoutRegistration registration = new SocketTimeoutRegistration(abortConnection, timer);
        if (!_registrations.TryAdd(connectionId, registration))
        {
            registration.Dispose();
        }
    }

    // Handshake Completion Cancellation - Disposes abandonment timer once connection binds to an authoritative host or player session.
    public void MarkAuthenticated(string connectionId)
    {
        if (_registrations.TryRemove(connectionId, out SocketTimeoutRegistration? registration))
        {
            registration.Dispose();
        }
    }

    // Socket Disconnect Eviction - Cleans up active registration when client closes transport prior to timeout expiration.
    public void Remove(string connectionId)
    {
        if (_registrations.TryRemove(connectionId, out SocketTimeoutRegistration? registration))
        {
            registration.Dispose();
        }
    }

    // Graceful Shutdown Abort (OPS-SHUT-001) - Disposes and aborts all pending unauthenticated sockets on server termination.
    public void AbortAll()
    {
        foreach (KeyValuePair<string, SocketTimeoutRegistration> entry in _registrations)
        {
            if (_registrations.TryRemove(entry.Key, out SocketTimeoutRegistration? registration))
            {
                registration.Abort();
                registration.Dispose();
            }
        }
    }

    // Handshake Timeout Callback - Forcefully aborts zombie socket if client fails to invoke JoinGame, Reconnect, or JoinAsHost within 15 seconds.
    private void OnTimeout(object? state)
    {
        if (state is not string connectionId)
        {
            return;
        }

        if (_registrations.TryRemove(connectionId, out SocketTimeoutRegistration? registration))
        {
            _logger.LogWarning(
                "Unauthenticated socket abandoned after 15 seconds; forcefully aborting connection. ConnectionId={ConnectionId}",
                connectionId);

            registration.Dispose();
            registration.Abort();
        }
    }

    // Socket Registration Lifetime Handle - Pairs socket abort delegate with underlying disposal timer.
    private sealed class SocketTimeoutRegistration : IDisposable
    {
        private readonly Action _abort;
        private readonly ITimer _timer;

        public SocketTimeoutRegistration(Action abort, ITimer timer)
        {
            _abort = abort;
            _timer = timer;
        }

        public void Abort() => _abort();

        public void Dispose() => _timer.Dispose();
    }
}
