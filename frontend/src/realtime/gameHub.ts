import {
  HttpTransportType,
  type HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';

import { authService } from '../api/authService.ts';
import type { PlayerGameStateResponse, RealtimeResponse } from './events.ts';

const SIGNALR_URL =
  import.meta.env.VITE_SIGNALR_URL ??
  import.meta.env.VITE_API_URL?.replace(/\/api\/?$/, '') ??
  'http://localhost:5000';

export function createGameHubConnection(requireHostAuth = false): HubConnection {
  const hubUrl = `${SIGNALR_URL}/hubs/game`;

  const builder = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      accessTokenFactory: requireHostAuth ? () => authService.getAccessToken() ?? '' : undefined,
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        if (retryContext.previousRetryCount === 0) return 0;
        if (retryContext.previousRetryCount < 3) return 2000;
        if (retryContext.previousRetryCount < 10) return 5000;
        return 10000;
      },
    })
    .configureLogging(LogLevel.Warning);

  return builder.build();
}

export async function invokeJoinAsHost(
  connection: HubConnection,
  gameId: string,
): Promise<RealtimeResponse<boolean>> {
  return await connection.invoke<RealtimeResponse<boolean>>('JoinAsHost', gameId);
}

export async function invokeReconnect(
  connection: HubConnection,
  sessionToken: string,
): Promise<RealtimeResponse<PlayerGameStateResponse>> {
  return await connection.invoke<RealtimeResponse<PlayerGameStateResponse>>(
    'Reconnect',
    sessionToken,
  );
}
