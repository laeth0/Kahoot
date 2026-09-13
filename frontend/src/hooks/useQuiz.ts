import { useCallback, useEffect, useRef, useState } from 'react';

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
  const reorderPendingRef = useRef(false);

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
      if (!quizId || !quiz || isMutating || reorderPendingRef.current) return;
      const questionsById = new Map(quiz.questions.map((question) => [question.id, question]));
      if (
        orderedQuestionIds.length !== quiz.questions.length ||
        new Set(orderedQuestionIds).size !== orderedQuestionIds.length ||
        orderedQuestionIds.some((id) => !questionsById.has(id))
      ) {
        throw new Error('The question list changed. Reload the quiz before reordering.');
      }
      const reorderedQuestions = orderedQuestionIds.map((id, orderIndex) => {
        const question = questionsById.get(id);
        if (!question) throw new Error('Question not found. Reload the quiz before reordering.');
        return { ...question, orderIndex };
      });
      reorderPendingRef.current = true;
      setIsMutating(true);
      setQuiz({ ...quiz, questions: reorderedQuestions, isPublished: false });
      try {
        await quizQuestionService.reorderQuestions(quizId, orderedQuestionIds);
      } catch (err) {
        setQuiz(quiz);
        throw err;
      } finally {
        reorderPendingRef.current = false;
        setIsMutating(false);
      }
    },
    [quizId, quiz, isMutating],
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
