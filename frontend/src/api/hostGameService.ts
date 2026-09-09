import type {
  GameParticipantResponse,
  GameStatus,
  HostQuestionResponse,
  LeaderboardResponse,
  QuestionResultsResponse,
} from '../realtime/events.ts';
import { axiosClient } from './axiosClient.ts';

export interface CreateGameResponse {
  gameId: string;
  pin: string;
  status: GameStatus;
}

export interface QuestionStartedResponse {
  host: HostQuestionResponse;
  player: unknown;
}

export interface HostGameStateResponse {
  gameId: string;
  pin: string;
  quizTitle: string;
  status: GameStatus;
  currentQuestionIndex: number | null;
  totalQuestions: number;
  currentQuestionStartedAt: string | null;
  currentQuestionEndsAt: string | null;
  answeredCount: number;
  participants: GameParticipantResponse[];
}

export const hostGameService = {
  async createGame(quizId: string): Promise<CreateGameResponse> {
    const response = await axiosClient.post<CreateGameResponse>('/games', { quizId });
    return response.data;
  },

  async getState(gameId: string): Promise<HostGameStateResponse> {
    const response = await axiosClient.get<HostGameStateResponse>(`/games/${gameId}`);
    return response.data;
  },

  async startGame(gameId: string): Promise<QuestionStartedResponse> {
    const response = await axiosClient.post<QuestionStartedResponse>(`/games/${gameId}/start`);
    return response.data;
  },

  async advance(gameId: string): Promise<QuestionStartedResponse> {
    const response = await axiosClient.post<QuestionStartedResponse>(`/games/${gameId}/advance`);
    return response.data;
  },

  async endQuestion(gameId: string): Promise<QuestionResultsResponse> {
    const response = await axiosClient.post<QuestionResultsResponse>(`/games/${gameId}/end-question`);
    return response.data;
  },

  async showLeaderboard(gameId: string): Promise<LeaderboardResponse> {
    const response = await axiosClient.post<LeaderboardResponse>(`/games/${gameId}/leaderboard`);
    return response.data;
  },

  async endGame(gameId: string): Promise<LeaderboardResponse> {
    const response = await axiosClient.post<LeaderboardResponse>(`/games/${gameId}/end`);
    return response.data;
  },

  async removeParticipant(gameId: string, participantId: string): Promise<void> {
    await axiosClient.delete(`/games/${gameId}/participants/${participantId}`);
  },

  async getQuestionResults(gameId: string, questionId: string): Promise<QuestionResultsResponse> {
    const response = await axiosClient.get<QuestionResultsResponse>(
      `/games/${gameId}/questions/${questionId}/results`,
    );
    return response.data;
  },

  async getLeaderboard(gameId: string): Promise<LeaderboardResponse> {
    const response = await axiosClient.get<LeaderboardResponse>(`/games/${gameId}/leaderboard`);
    return response.data;
  },
};
