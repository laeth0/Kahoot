import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import ImageIcon from '@mui/icons-material/Image';
import TimerOutlinedIcon from '@mui/icons-material/TimerOutlined';
import { Box, CardContent, Chip, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

import type { QuestionResponse } from '../../api/quizService.ts';

interface QuestionCardContentProps {
  question: QuestionResponse;
  position: number;
  dragHandle?: ReactNode;
  actions?: ReactNode;
}

export function QuestionCardContent({
  question,
  position,
  dragHandle,
  actions,
}: QuestionCardContentProps) {
  const correctChoices = question.choices.filter((choice) => choice.isCorrect);
  return (
    <CardContent sx={{ p: { xs: 2, sm: 2.5 }, '&:last-child': { pb: { xs: 2, sm: 2.5 } } }}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ alignItems: { sm: 'flex-start' }, justifyContent: 'space-between' }}
      >
        <Stack
          direction="row"
          sx={{ flex: 1, minWidth: 0, flexWrap: { xs: 'wrap', sm: 'nowrap' }, gap: 2 }}
        >
          {dragHandle}
          <Box
            sx={{
              minWidth: 42,
              px: 1,
              height: 42,
              borderRadius: 2,
              bgcolor: '#00629b',
              color: '#ffffff',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontWeight: 800,
              fontSize: '1.1rem',
              flexShrink: 0,
            }}
          >
            #{position}
          </Box>

          <Box sx={{ flex: 1, minWidth: 0, flexBasis: { xs: '100%', sm: 'auto' } }}>
            <Stack
              direction="row"
              spacing={1}
              sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1, mb: 1 }}
            >
              <Chip
                icon={<TimerOutlinedIcon sx={{ fontSize: 16 }} />}
                label={`${question.timeLimitSeconds}s`}
                size="small"
                sx={{
                  bgcolor: '#f0f4f8',
                  color: '#334e68',
                  fontWeight: 700,
                  fontSize: '0.75rem',
                }}
              />
              <Chip
                label={`${question.points} pts`}
                size="small"
                sx={{
                  bgcolor: '#e0f2fe',
                  color: '#0284c7',
                  fontWeight: 700,
                  fontSize: '0.75rem',
                }}
              />
              <Chip
                label={`${question.choices.length} choices`}
                size="small"
                sx={{
                  bgcolor: '#f8fafc',
                  color: '#627d98',
                  fontWeight: 600,
                  fontSize: '0.75rem',
                }}
              />
              {question.imageUrl && (
                <Chip
                  icon={<ImageIcon sx={{ fontSize: 16 }} />}
                  label="Image attached"
                  size="small"
                  sx={{
                    bgcolor: '#f1f5f9',
                    color: '#475569',
                    fontWeight: 600,
                    fontSize: '0.75rem',
                  }}
                />
              )}
            </Stack>

            <Typography
              variant="body1"
              sx={{
                fontWeight: 700,
                color: '#09131f',
                lineHeight: 1.4,
                mb: 1.5,
                wordBreak: 'break-word',
              }}
            >
              {question.text}
            </Typography>

            {correctChoices.length > 0 && (
              <Stack
                direction="row"
                spacing={1}
                sx={{
                  alignItems: 'center',
                  bgcolor: 'rgba(16, 185, 129, 0.08)',
                  p: 1,
                  borderRadius: 1.5,
                  border: '1px solid rgba(16, 185, 129, 0.2)',
                  maxWidth: 540,
                }}
              >
                <CheckCircleOutlinedIcon sx={{ color: '#10b981', fontSize: 18 }} />
                <Typography
                  variant="caption"
                  sx={{
                    fontWeight: 700,
                    color: '#065f46',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                  }}
                >
                  {correctChoices.length > 1 ? 'Correct answers: ' : 'Correct: '}
                  {correctChoices.map((c) => c.text || '(Image answer)').join(', ')}
                </Typography>
              </Stack>
            )}
          </Box>
        </Stack>

        {actions}
      </Stack>
    </CardContent>
  );
}
