import BoltIcon from '@mui/icons-material/Bolt';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HighlightOffIcon from '@mui/icons-material/HighlightOff';
import HourglassTopIcon from '@mui/icons-material/HourglassTop';
import { Box, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

export type AnswerFeedbackVariant =
  'accepted' | 'alreadyAnswered' | 'tooLate' | 'rejected' | 'slowDown';

interface FeedbackContent {
  icon: ReactNode;
  tint: string;
  headline: string;
  detail: string;
  showWaiting: boolean;
}

const CONTENT: Record<AnswerFeedbackVariant, FeedbackContent> = {
  accepted: {
    icon: <CheckCircleIcon sx={{ fontSize: 34 }} />,
    tint: '#059669',
    headline: 'Answer locked in!',
    detail: 'Sit tight while everyone else answers.',
    showWaiting: true,
  },
  alreadyAnswered: {
    icon: <CheckCircleIcon sx={{ fontSize: 34 }} />,
    tint: '#059669',
    headline: "You're already in",
    detail: 'Your first answer is the one that counts.',
    showWaiting: true,
  },
  tooLate: {
    icon: <HourglassTopIcon sx={{ fontSize: 34 }} />,
    tint: '#B45309',
    headline: 'Time is up',
    detail: 'That answer did not make the deadline. Waiting for results…',
    showWaiting: true,
  },
  rejected: {
    icon: <HighlightOffIcon sx={{ fontSize: 34 }} />,
    tint: '#64748B',
    headline: "That didn't go through",
    detail: 'Give it another tap while the question is still open.',
    showWaiting: false,
  },
  slowDown: {
    icon: <BoltIcon sx={{ fontSize: 34 }} />,
    tint: '#B45309',
    headline: 'Slow down a moment',
    detail: 'Wait a second, then pick your answer.',
    showWaiting: false,
  },
};

export interface AnswerFeedbackScreenProps {
  variant: AnswerFeedbackVariant;
}

export function AnswerFeedbackScreen({ variant }: AnswerFeedbackScreenProps) {
  const content = CONTENT[variant];

  return (
    <Paper
      elevation={0}
      role="status"
      sx={{
        borderRadius: 4,
        border: '1px solid #E2E8F0',
        bgcolor: '#FFFFFF',
        p: { xs: 3, sm: 4 },
        textAlign: 'center',
      }}
    >
      <Stack spacing={2} sx={{ alignItems: 'center' }}>
        <Box
          sx={{
            width: 64,
            height: 64,
            borderRadius: '50%',
            bgcolor: '#F1F5F9',
            color: content.tint,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          {content.icon}
        </Box>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131F' }}>
          {content.headline}
        </Typography>
        <Typography variant="body1" sx={{ color: '#486581', lineHeight: 1.6 }}>
          {content.detail}
        </Typography>

        {content.showWaiting && (
          <Stack direction="row" spacing={0.75} aria-hidden sx={{ pt: 0.5 }}>
            {[0, 1, 2].map((dot) => (
              <Box
                key={dot}
                sx={{
                  width: 10,
                  height: 10,
                  borderRadius: '50%',
                  bgcolor: '#00629B',
                  animation: `feedbackBlink 1.2s ease-in-out ${dot * 0.2}s infinite`,
                  '@keyframes feedbackBlink': {
                    '0%, 100%': { opacity: 0.25 },
                    '50%': { opacity: 1 },
                  },
                  '@media (prefers-reduced-motion: reduce)': { animation: 'none', opacity: 0.5 },
                }}
              />
            ))}
          </Stack>
        )}
      </Stack>
    </Paper>
  );
}

export default AnswerFeedbackScreen;
