import { axiosClient } from './axiosClient.ts';

export interface CreateGameResponse {
  gameId: string;
  pin: string;
  status: string;
}

export const hostGameService = {
  async createGame(quizId: string): Promise<CreateGameResponse> {
    const response = await axiosClient.post<CreateGameResponse>('/games', { quizId });
    return response.data;
  },
};
