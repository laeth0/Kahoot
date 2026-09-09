import { type HubConnection, HubConnectionState } from '@microsoft/signalr';
import { useEffect, useRef, useState } from 'react';

import { createGameHubConnection } from '../realtime/gameHub.ts';

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export function useGameHubConnection(requireHostAuth = false) {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');
  const connectionRef = useRef<HubConnection | null>(null);
  const [retryTrigger, setRetryTrigger] = useState(0);

  useEffect(() => {
    let isCancelled = false;
    const connection = createGameHubConnection(requireHostAuth);
    connectionRef.current = connection;

    setStatus('connecting');

    connection.onreconnecting(() => {
      if (!isCancelled) setStatus('reconnecting');
    });

    connection.onreconnected(() => {
      if (!isCancelled) setStatus('connected');
    });

    connection.onclose(() => {
      if (!isCancelled) setStatus('disconnected');
    });

    connection
      .start()
      .then(() => {
        if (!isCancelled) setStatus('connected');
      })
      .catch(() => {
        if (!isCancelled) setStatus('disconnected');
      });

    return () => {
      isCancelled = true;
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop().catch(() => {});
      }
      connectionRef.current = null;
    };
  }, [requireHostAuth, retryTrigger]);

  const retry = () => {
    setRetryTrigger((prev) => prev + 1);
  };

  return {
    connection: connectionRef.current,
    status,
    retry,
  };
}

export default useGameHubConnection;
