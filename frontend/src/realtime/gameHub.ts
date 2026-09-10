import {
  HttpTransportType,
  type HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';

import { authService } from '../api/authService.ts';
import type { AnswerAckResponse, PlayerGameStateResponse, RealtimeResponse } from './events.ts';

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
        const count = retryContext.previousRetryCount;
        if (count === 0) return 0;
        const jitter = Math.floor(Math.random() * 1000);
        if (count < 3) return 1500 + jitter;
        if (count < 8) return 3500 + jitter;
        if (count < 15) return 7000 + jitter;
        return 12000 + jitter;
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

export async function invokeSubmitAnswer(
  connection: HubConnection,
  questionId: string,
  selectedChoiceId: string,
): Promise<RealtimeResponse<AnswerAckResponse>> {
  return await connection.invoke<RealtimeResponse<AnswerAckResponse>>(
    'SubmitAnswer',
    questionId,
    selectedChoiceId,
  );
}
