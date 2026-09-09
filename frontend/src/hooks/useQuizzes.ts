import { useCallback, useEffect, useState } from 'react';

import { quizService, type QuizSummaryResponse } from '../api/quizService.ts';

export function useQuizzes() {
  const [quizzes, setQuizzes] = useState<QuizSummaryResponse[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshIndex, setRefreshIndex] = useState(0);

  useEffect(() => {
    let active = true;
    quizService
      .listQuizzes()
      .then((data) => {
        if (active) {
          setQuizzes(data);
          setError(null);
          setIsLoading(false);
        }
      })
      .catch((err) => {
        if (active) {
          const msg = err instanceof Error ? err.message : 'Failed to load quizzes';
          setError(msg);
          setIsLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [refreshIndex]);

  const refetch = useCallback(() => {
    setIsLoading(true);
    setRefreshIndex((prev) => prev + 1);
  }, []);

  const deleteQuiz = useCallback(async (id: string) => {
    await quizService.deleteQuiz(id);
    setQuizzes((prev) => prev.filter((q) => q.id !== id));
  }, []);

  return {
    quizzes,
    isLoading,
    error,
    refetch,
    deleteQuiz,
  };
}

export default useQuizzes;
