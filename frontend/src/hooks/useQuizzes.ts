import { useCallback, useEffect, useState } from 'react';

import { type QuizSummaryResponse, quizService } from '../api/quizService.ts';

export function useQuizzes() {
  const [quizzes, setQuizzes] = useState<QuizSummaryResponse[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchQuizzes = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await quizService.listQuizzes();
      setQuizzes(data);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to load quizzes';
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchQuizzes();
  }, [fetchQuizzes]);

  const deleteQuiz = useCallback(async (id: string) => {
    await quizService.deleteQuiz(id);
    setQuizzes((prev) => prev.filter((q) => q.id !== id));
  }, []);

  return {
    quizzes,
    isLoading,
    error,
    refetch: fetchQuizzes,
    deleteQuiz,
  };
}

export default useQuizzes;
