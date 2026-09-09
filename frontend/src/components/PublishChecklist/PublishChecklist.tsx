import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HighlightOffIcon from '@mui/icons-material/HighlightOff';
import { Box, Paper, Stack, Typography } from '@mui/material';

import type { QuestionResponse } from '../../api/quizService.ts';
import { evaluateQuizPublishCriteria } from './checklistUtils.ts';

export interface PublishChecklistProps {
  questions: QuestionResponse[];
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
