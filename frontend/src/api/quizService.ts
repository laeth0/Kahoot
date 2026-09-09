import { axiosClient } from './axiosClient.ts';

export interface QuizSummaryResponse {
  id: string;
  title: string;
  description: string | null;
  isPublished: boolean;
  questionCount: number;
}

export interface ChoiceResponse {
  id: string;
  orderIndex: number;
  text: string | null;
  imageUrl: string | null;
  isCorrect: boolean;
}

export interface QuestionResponse {
  id: string;
  orderIndex: number;
  text: string;
  imageUrl: string | null;
  timeLimitSeconds: number;
  points: number;
  choices: ChoiceResponse[];
}

export interface QuizDetailResponse {
  id: string;
  title: string;
  description: string | null;
  isPublished: boolean;
  questions: QuestionResponse[];
}

export interface CreateQuizPayload {
  title: string;
  description?: string | null;
}

export interface UpdateQuizPayload {
  title: string;
  description?: string | null;
}

export const quizService = {
  async listQuizzes(): Promise<QuizSummaryResponse[]> {
    const response = await axiosClient.get<QuizSummaryResponse[]>('/quizzes');
    return response.data;
  },

  async getQuiz(id: string): Promise<QuizDetailResponse> {
    const response = await axiosClient.get<QuizDetailResponse>(`/quizzes/${id}`);
    return response.data;
  },

  async createQuiz(payload: CreateQuizPayload): Promise<string> {
    const response = await axiosClient.post<{ id: string }>('/quizzes', payload);
    return response.data.id;
  },

  async updateQuiz(id: string, payload: UpdateQuizPayload): Promise<void> {
    await axiosClient.put(`/quizzes/${id}`, payload);
  },

  async deleteQuiz(id: string): Promise<void> {
    await axiosClient.delete(`/quizzes/${id}`);
  },

  async publishQuiz(id: string): Promise<void> {
    await axiosClient.post(`/quizzes/${id}/publish`);
  },
};
