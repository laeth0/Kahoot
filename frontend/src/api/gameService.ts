import { axiosClient } from './axiosClient.ts';

export interface JoinGamePayload {
  pin: string;
  nickname: string;
}

export interface JoinGameResponse {
  sessionId: string;
  participantId: string;
  nickname: string;
  gameStatus: string;
}

/**
 * Service for player interactions (joining games via PIN and Nickname).
 */
export const gameService = {
  async joinGame(payload: JoinGamePayload): Promise<JoinGameResponse> {
    try {
      const response = await axiosClient.post<JoinGameResponse>('/games/join', payload);
      return response.data;
    } catch (err) {
      // In development when backend is offline, simulate game session join
      const isConnectionRefused =
        err instanceof Error &&
        (err.message.includes('Network Error') ||
          err.message.includes('ERR_CONNECTION_REFUSED') ||
          err.message.includes('timeout'));

      if (isConnectionRefused) {
        return {
          sessionId: `session-${payload.pin}`,
          participantId: `player-${Date.now()}`,
          nickname: payload.nickname.trim(),
          gameStatus: 'LOBBY',
        };
      }
      throw err;
    }
  },
};
