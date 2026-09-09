import { useCallback, useEffect, useState } from 'react';

import { type SaveQuestionPayload, quizQuestionService } from '../api/quizQuestionService.ts';
import {
  type QuizDetailResponse,
  quizService,
  type UpdateQuizPayload,
} from '../api/quizService.ts';

export function useQuiz(quizId: string | undefined) {
  const [quiz, setQuiz] = useState<QuizDetailResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [isMutating, setIsMutating] = useState<boolean>(false);

  const fetchQuiz = useCallback(async () => {
    if (!quizId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await quizService.getQuiz(quizId);
      setQuiz(data);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to load quiz';
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, [quizId]);

  useEffect(() => {
    fetchQuiz();
  }, [fetchQuiz]);

  const updateMetadata = useCallback(
    async (payload: UpdateQuizPayload) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizService.updateQuiz(quizId, payload);
        setQuiz((prev) =>
          prev
            ? {
                ...prev,
                title: payload.title,
                description: payload.description ?? null,
                isPublished: false,
              }
            : null,
        );
      } finally {
        setIsMutating(false);
      }
    },
    [quizId],
  );

  const publish = useCallback(async () => {
    if (!quizId) return;
    setIsMutating(true);
    try {
      await quizService.publishQuiz(quizId);
      setQuiz((prev) => (prev ? { ...prev, isPublished: true } : null));
    } finally {
      setIsMutating(false);
    }
  }, [quizId]);

  const addQuestion = useCallback(
    async (payload: SaveQuestionPayload) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizQuestionService.addQuestion(quizId, payload);
        await fetchQuiz();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, fetchQuiz],
  );

  const updateQuestion = useCallback(
    async (questionId: string, payload: SaveQuestionPayload) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizQuestionService.updateQuestion(quizId, questionId, payload);
        await fetchQuiz();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, fetchQuiz],
  );

  const deleteQuestion = useCallback(
    async (questionId: string) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizQuestionService.deleteQuestion(quizId, questionId);
        await fetchQuiz();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, fetchQuiz],
  );

  const reorderQuestions = useCallback(
    async (orderedQuestionIds: string[]) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizQuestionService.reorderQuestions(quizId, orderedQuestionIds);
        await fetchQuiz();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, fetchQuiz],
  );

  return {
    quiz,
    isLoading,
    error,
    isMutating,
    refetch: fetchQuiz,
    updateMetadata,
    publish,
    addQuestion,
    updateQuestion,
    deleteQuestion,
    reorderQuestions,
  };
}

export default useQuiz;
