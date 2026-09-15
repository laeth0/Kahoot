import {
  HttpTransportType,
  type HubConnection,
  HubConnectionBuilder,
  type ILogger,
  LogLevel,
} from '@microsoft/signalr';

import { authService } from '../api/authService.ts';
import type { AnswerAckResponse, PlayerGameStateResponse, RealtimeResponse } from './events.ts';

const signalRLogger: ILogger = {
  log(logLevel: LogLevel, message: string) {
    if (message.includes('The connection was stopped during negotiation')) {
      return;
    }
    if (logLevel >= LogLevel.Error) {
      console.error(message);
    } else if (logLevel >= LogLevel.Warning) {
      console.warn(message);
    }
  },
};

function getHubUrl(): string {
  const configuredSignalR = import.meta.env.VITE_SIGNALR_URL?.trim();
  if (configuredSignalR) {
    return configuredSignalR.endsWith('/hubs/game')
      ? configuredSignalR
      : `${configuredSignalR.replace(/\/$/, '')}/hubs/game`;
  }

  const configuredApi = import.meta.env.VITE_API_URL?.trim();
  if (configuredApi) {
    const base = configuredApi.replace(/\/api\/?$/, '').replace(/\/$/, '');
    return base ? `${base}/hubs/game` : '/hubs/game';
  }

  return '/hubs/game';
}

export function createGameHubConnection(requireHostAuth = false): HubConnection {
  const hubUrl = getHubUrl();

  const builder = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      accessTokenFactory: requireHostAuth
        ? () => authService.getAccessTokenForConnection()
        : undefined,
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
    .configureLogging(signalRLogger);

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
  selectedChoiceIds: string | string[],
): Promise<RealtimeResponse<AnswerAckResponse>> {
  const payload = Array.isArray(selectedChoiceIds) ? selectedChoiceIds : [selectedChoiceIds];
  return await connection.invoke<RealtimeResponse<AnswerAckResponse>>(
    'SubmitAnswer',
    questionId,
    payload,
  );
}
