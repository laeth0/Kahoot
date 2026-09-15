using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.Presence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kahoot.Infrastructure.Games;

public sealed class HostPresenceTracker(
    IOptions<GameLifecycleOptions> options,
    ILogger<HostPresenceTracker> logger) : IHostPresenceTracker, ISingletonService
{
    private readonly ConcurrentDictionary<Guid, HostPresenceState> _games = new();

    public void HostConnected(Guid gameId, string connectionId)
    {
        HostPresenceState state = _games.GetOrAdd(gameId, _ => new HostPresenceState());
        lock (state.SyncRoot)
        {
            if (state.DisconnectCts is not null)
            {
                state.DisconnectCts.Cancel();
                state.DisconnectCts.Dispose();
                state.DisconnectCts = null;
            }

            state.ConnectionIds.Add(connectionId);
        }
    }

    public void HostDisconnected(Guid gameId, string connectionId, Func<Guid, Task> onTimeout)
    {
        if (!_games.TryGetValue(gameId, out HostPresenceState? state))
        {
            return;
        }

        CancellationTokenSource? ctsToStart = null;
        lock (state.SyncRoot)
        {
            state.ConnectionIds.Remove(connectionId);
            if (state.ConnectionIds.Count == 0)
            {
                if (state.DisconnectCts is not null)
                {
                    state.DisconnectCts.Cancel();
                    state.DisconnectCts.Dispose();
                }

                state.DisconnectCts = new CancellationTokenSource();
                ctsToStart = state.DisconnectCts;
            }
        }

        if (ctsToStart is not null)
        {
            int gracePeriodSeconds = options.Value.HostDisconnectGracePeriodSeconds;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(gracePeriodSeconds), ctsToStart.Token);
                    await onTimeout(gameId);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Error during host disconnect grace period timeout for game {GameId}", gameId);
                }
            });
        }
    }

    public bool IsHostConnectedOrInGracePeriod(Guid gameId)
    {
        if (!_games.TryGetValue(gameId, out HostPresenceState? state))
        {
            return false;
        }

        lock (state.SyncRoot)
        {
            return state.ConnectionIds.Count > 0 ||
                   (state.DisconnectCts is not null && !state.DisconnectCts.IsCancellationRequested);
        }
    }

    public void RemoveGame(Guid gameId)
    {
        if (!_games.TryRemove(gameId, out HostPresenceState? state))
        {
            return;
        }

        lock (state.SyncRoot)
        {
            if (state.DisconnectCts is not null)
            {
                state.DisconnectCts.Cancel();
                state.DisconnectCts.Dispose();
                state.DisconnectCts = null;
            }

            state.ConnectionIds.Clear();
        }
    }

    private sealed class HostPresenceState
    {
        public object SyncRoot { get; } = new();

        public HashSet<string> ConnectionIds { get; } = [];

        public CancellationTokenSource? DisconnectCts { get; set; }
    }
}
