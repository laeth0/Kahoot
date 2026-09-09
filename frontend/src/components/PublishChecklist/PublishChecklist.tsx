import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HighlightOffIcon from '@mui/icons-material/HighlightOff';
import { Box, Paper, Stack, Typography } from '@mui/material';

import type { QuestionResponse } from '../../api/quizService.ts';

export interface PublishChecklistProps {
  questions: QuestionResponse[];
}

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

export function PublishChecklist({ questions }: PublishChecklistProps) {
  const { items, isPublishable } = evaluateQuizPublishCriteria(questions);

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        borderRadius: 2.5,
        border: '1px solid #e2e8f0',
        bgcolor: isPublishable ? '#f0fdf4' : '#ffffff',
      }}
    >
      <Typography
        variant="subtitle2"
        sx={{
          fontWeight: 700,
          color: isPublishable ? '#15803d' : '#09131f',
          mb: 1.5,
        }}
      >
        {isPublishable ? 'Ready to Publish & Host' : 'Publishing Requirements Checklist (FR-3.2)'}
      </Typography>

      <Stack spacing={1.25}>
        {items.map((item) => (
          <Stack key={item.id} direction="row" spacing={1.25} sx={{ alignItems: 'flex-start' }}>
            {item.passed ? (
              <CheckCircleIcon sx={{ fontSize: 18, color: '#16a34a', mt: 0.2 }} />
            ) : (
              <HighlightOffIcon sx={{ fontSize: 18, color: '#dc2626', mt: 0.2 }} />
            )}
            <Box>
              <Typography
                variant="body2"
                sx={{
                  color: item.passed ? '#15803d' : '#334e68',
                  fontWeight: item.passed ? 600 : 500,
                  lineHeight: 1.4,
                }}
              >
                {item.label}
              </Typography>
              {!item.passed && item.failingQuestions && item.failingQuestions.length > 0 && (
                <Typography variant="caption" sx={{ color: '#dc2626', display: 'block' }}>
                  Issue on question(s): #{item.failingQuestions.join(', #')}
                </Typography>
              )}
            </Box>
          </Stack>
        ))}
      </Stack>
    </Paper>
  );
}

export default PublishChecklist;
