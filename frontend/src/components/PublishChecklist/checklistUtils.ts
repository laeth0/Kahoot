import type { QuestionResponse } from '../../api/quizService.ts';

export interface ChecklistItem {
  id: string;
  label: string;
  passed: boolean;
  failingQuestions?: number[];
}

export function evaluateQuizPublishCriteria(questions: QuestionResponse[]): {
  items: ChecklistItem[];
  isPublishable: boolean;
} {
  const hasQuestions = questions.length > 0;

  const failingChoiceCount: number[] = [];
  const failingCorrectCount: number[] = [];
  const failingChoiceContent: number[] = [];
  const failingTimeLimit: number[] = [];

  questions.forEach((q, index) => {
    const qNum = index + 1;

    if (q.choices.length < 2 || q.choices.length > 6) {
      failingChoiceCount.push(qNum);
    }

    const correctCount = q.choices.filter((c) => c.isCorrect).length;
    if (correctCount !== 1) {
      failingCorrectCount.push(qNum);
    }

    const allChoicesHaveContent = q.choices.every(
      (c) => Boolean(c.text && c.text.trim().length > 0) || Boolean(c.imageUrl),
    );
    if (!allChoicesHaveContent) {
      failingChoiceContent.push(qNum);
    }

    if (q.timeLimitSeconds < 5 || q.timeLimitSeconds > 300) {
      failingTimeLimit.push(qNum);
    }
  });

  const items: ChecklistItem[] = [
    {
      id: 'has-questions',
      label: 'At least 1 question added',
      passed: hasQuestions,
    },
    {
      id: 'choice-count',
      label: 'Every question has 2 to 6 choices',
      passed: hasQuestions && failingChoiceCount.length === 0,
      failingQuestions: failingChoiceCount,
    },
    {
      id: 'one-correct',
      label: 'Exactly one correct choice marked per question',
      passed: hasQuestions && failingCorrectCount.length === 0,
      failingQuestions: failingCorrectCount,
    },
    {
      id: 'choice-content',
      label: 'Every choice has text or an image attached',
      passed: hasQuestions && failingChoiceContent.length === 0,
      failingQuestions: failingChoiceContent,
    },
    {
      id: 'time-limit',
      label: 'Question time limits between 5 and 300 seconds',
      passed: hasQuestions && failingTimeLimit.length === 0,
      failingQuestions: failingTimeLimit,
    },
  ];

  const isPublishable = items.every((item) => item.passed);

  return { items, isPublishable };
}
