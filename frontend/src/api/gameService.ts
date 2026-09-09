import { axiosClient } from './axiosClient.ts';

export interface JoinGamePayload {
  pin: string;
  nickname: string;
}

/** Server game-state values (must match `Kahoot.Domain.Games.GameStatus`). */
export type GameStatus =
  | 'Created'
  | 'Lobby'
  | 'QuestionActive'
  | 'QuestionResults'
  | 'Leaderboard'
  | 'Finished';

export interface JoinGameResponse {
  gameId: string;
  participantId: string;
  /** One-time reconnection token; the client keeps it to re-join over SignalR. */
  sessionToken: string;
  nickname: string;
  status: GameStatus;
}

/** Player bootstrap: join a live game via PIN + nickname before opening the socket. */
export const gameService = {
  async joinGame(payload: JoinGamePayload): Promise<JoinGameResponse> {
    const response = await axiosClient.post<JoinGameResponse>('/games/join', {
      pin: payload.pin.trim(),
      nickname: payload.nickname.trim(),
    });
    return response.data;
  },
};
