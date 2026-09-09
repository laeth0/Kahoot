import { axiosClient } from './axiosClient.ts';

export interface JoinGamePayload {
  pin: string;
  nickname: string;
}

export type GameStatus =
  'Created' | 'Lobby' | 'QuestionActive' | 'QuestionResults' | 'Leaderboard' | 'Finished';

export interface JoinGameResponse {
  gameId: string;
  participantId: string;
  sessionToken: string;
  nickname: string;
  status: GameStatus;
}

export const gameService = {
  async joinGame(payload: JoinGamePayload): Promise<JoinGameResponse> {
    const response = await axiosClient.post<JoinGameResponse>('/games/join', {
      pin: payload.pin.trim(),
      nickname: payload.nickname.trim(),
    });
    return response.data;
  },
};
