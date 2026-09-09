import { axiosClient } from './axiosClient.ts';

export interface ChoiceInput {
  text: string | null;
  imageUrl: string | null;
  isCorrect: boolean;
}

export interface SaveQuestionPayload {
  text: string;
  imageUrl?: string | null;
  timeLimitSeconds: number;
  points: number;
  choices: ChoiceInput[];
}

export const quizQuestionService = {
  async addQuestion(quizId: string, payload: SaveQuestionPayload): Promise<string> {
    const response = await axiosClient.post<{ id: string }>(
      `/quizzes/${quizId}/questions`,
      payload,
    );
    return response.data.id;
  },

  async updateQuestion(
    quizId: string,
    questionId: string,
    payload: SaveQuestionPayload,
  ): Promise<void> {
    await axiosClient.put(`/quizzes/${quizId}/questions/${questionId}`, payload);
  },

  async deleteQuestion(quizId: string, questionId: string): Promise<void> {
    await axiosClient.delete(`/quizzes/${quizId}/questions/${questionId}`);
  },

  async reorderQuestions(quizId: string, orderedQuestionIds: string[]): Promise<void> {
    await axiosClient.put(`/quizzes/${quizId}/questions/order`, { orderedQuestionIds });
  },
};
