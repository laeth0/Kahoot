import { useCallback, useEffect, useState } from 'react';

import { quizQuestionService, type SaveQuestionPayload } from '../api/quizQuestionService.ts';
import {
  type QuizDetailResponse,
  quizService,
  type UpdateQuizPayload,
} from '../api/quizService.ts';

export function useQuiz(quizId: string | undefined) {
  const [quiz, setQuiz] = useState<QuizDetailResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(() => Boolean(quizId));
  const [error, setError] = useState<string | null>(null);
  const [isMutating, setIsMutating] = useState<boolean>(false);
  const [refreshIndex, setRefreshIndex] = useState(0);

  useEffect(() => {
    if (!quizId) {
      return;
    }

    let active = true;
    quizService
      .getQuiz(quizId)
      .then((data) => {
        if (active) {
          setQuiz(data);
          setError(null);
          setIsLoading(false);
        }
      })
      .catch((err) => {
        if (active) {
          const msg = err instanceof Error ? err.message : 'Failed to load quiz';
          setError(msg);
          setIsLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [quizId, refreshIndex]);

  const refetch = useCallback(async () => {
    if (!quizId) return;
    try {
      const data = await quizService.getQuiz(quizId);
      setQuiz(data);
      setError(null);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to load quiz';
      setError(msg);
    }
  }, [quizId]);

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
        await refetch();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, refetch],
  );

  const updateQuestion = useCallback(
    async (questionId: string, payload: SaveQuestionPayload) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizQuestionService.updateQuestion(quizId, questionId, payload);
        await refetch();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, refetch],
  );

  const deleteQuestion = useCallback(
    async (questionId: string) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizQuestionService.deleteQuestion(quizId, questionId);
        await refetch();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, refetch],
  );

  const reorderQuestions = useCallback(
    async (orderedQuestionIds: string[]) => {
      if (!quizId) return;
      setIsMutating(true);
      try {
        await quizQuestionService.reorderQuestions(quizId, orderedQuestionIds);
        await refetch();
      } finally {
        setIsMutating(false);
      }
    },
    [quizId, refetch],
  );

  const triggerReload = useCallback(() => {
    setIsLoading(true);
    setRefreshIndex((prev) => prev + 1);
  }, []);

  return {
    quiz,
    isLoading,
    error,
    isMutating,
    refetch: triggerReload,
    updateMetadata,
    publish,
    addQuestion,
    updateQuestion,
    deleteQuestion,
    reorderQuestions,
  };
}

export default useQuiz;
