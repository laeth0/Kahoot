import type {
  GameParticipantResponse,
  GameStatus,
  HostQuestionResponse,
  LeaderboardResponse,
  PlayerQuestionResponse,
  QuestionResultsResponse,
} from '../realtime/events.ts';
import { axiosClient } from './axiosClient.ts';

export interface CreateGameResponse {
  gameId: string;
  pin: string;
  status: GameStatus;
  joinUrl?: string;
}

export interface QuestionStartedResponse {
  host: HostQuestionResponse;
  player: PlayerQuestionResponse;
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
  participantCount: number;
  presenceVersion: number;
  participants: GameParticipantResponse[];
  joinUrl?: string;
}

export const hostGameService = {
  async createGame(quizId: string): Promise<CreateGameResponse> {
    const response = await axiosClient.post<CreateGameResponse>('/games', { quizId });
    return response.data;
  },

  async getState(gameId: string, signal?: AbortSignal): Promise<HostGameStateResponse> {
    const response = await axiosClient.get<HostGameStateResponse>(`/games/${gameId}`, { signal });
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
    const response = await axiosClient.post<QuestionResultsResponse>(
      `/games/${gameId}/end-question`,
    );
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

  async getQuestionResults(
    gameId: string,
    questionId: string,
    signal?: AbortSignal,
  ): Promise<QuestionResultsResponse> {
    const response = await axiosClient.get<QuestionResultsResponse>(
      `/games/${gameId}/questions/${questionId}/results`,
      { signal },
    );
    return response.data;
  },

  async getLeaderboard(gameId: string, signal?: AbortSignal): Promise<LeaderboardResponse> {
    const response = await axiosClient.get<LeaderboardResponse>(`/games/${gameId}/leaderboard`, {
      signal,
    });
    return response.data;
  },
};
