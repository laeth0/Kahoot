import { type HubConnection, HubConnectionState } from '@microsoft/signalr';
import { useCallback, useEffect, useState } from 'react';

import { createGameHubConnection } from '../realtime/gameHub.ts';

export type HubConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export function useGameHubConnection(requireHostAuth = false) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [status, setStatus] = useState<HubConnectionStatus>('connecting');
  const [retryTrigger, setRetryTrigger] = useState(0);

  useEffect(() => {
    let isCancelled = false;
    const hubConnection = createGameHubConnection(requireHostAuth);

    hubConnection.onreconnecting(() => {
      if (!isCancelled) setStatus('reconnecting');
    });

    hubConnection.onreconnected(() => {
      if (!isCancelled) setStatus('connected');
    });

    hubConnection.onclose(() => {
      if (!isCancelled) {
        setStatus('disconnected');
        setConnection(null);
      }
    });

    hubConnection
      .start()
      .then(() => {
        if (!isCancelled) {
          setConnection(hubConnection);
          setStatus('connected');
        }
      })
      .catch(() => {
        if (!isCancelled) {
          setConnection(null);
          setStatus('disconnected');
        }
      });

    return () => {
      isCancelled = true;
      if (hubConnection.state !== HubConnectionState.Disconnected) {
        hubConnection.stop().catch(() => {});
      }
      setConnection(null);
    };
  }, [requireHostAuth, retryTrigger]);

  const retry = useCallback(() => {
    setStatus('connecting');
    setRetryTrigger((prev) => prev + 1);
  }, []);

  return {
    connection,
    status,
    retry,
  };
}

export default useGameHubConnection;
