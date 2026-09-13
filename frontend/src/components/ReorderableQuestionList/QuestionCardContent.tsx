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
  return (
    <CardContent
      sx={{
        py: { xs: 1.75, sm: 2 },
        px: { xs: 2, sm: 2.5 },
        '&:last-child': { pb: { xs: 1.75, sm: 2 } },
      }}
    >
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between' }}
      >
        <Stack
          direction="row"
          sx={{
            flex: 1,
            minWidth: 0,
            flexWrap: { xs: 'wrap', sm: 'nowrap' },
            gap: 2,
            alignItems: 'center',
          }}
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
              sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1, mb: 0.75 }}
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
                wordBreak: 'break-word',
              }}
            >
              {question.text}
            </Typography>
          </Box>
        </Stack>

        {actions}
      </Stack>
    </CardContent>
  );
}
