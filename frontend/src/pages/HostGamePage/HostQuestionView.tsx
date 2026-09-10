import { Box, Card, Stack, Typography } from '@mui/material';

import { AnsweredCounter } from '../../components/AnsweredCounter/index.ts';
import { ChoiceGrid } from '../../components/ChoiceGrid/index.ts';
import { QuestionMedia } from '../../components/QuestionMedia/index.ts';
import { ServerCountdown } from '../../components/ServerCountdown/index.ts';
import { useServerCountdown } from '../../hooks/useServerCountdown.ts';
import type { HostQuestionResponse } from '../../realtime/events.ts';

export interface HostQuestionViewProps {
  question: HostQuestionResponse | null;
  endsAt: string | null;
  startedAt: string | null;
  answeredCount: number;
  participantCount: number;
  paused: boolean;
}

export function HostQuestionView({
  question,
  endsAt,
  startedAt,
  answeredCount,
  participantCount,
  paused,
}: HostQuestionViewProps) {
  const countdown = useServerCountdown(endsAt, {
    startedAt,
    totalSeconds: question?.timeLimitSeconds ?? 30,
    paused,
  });

  return (
    <Card
      sx={{
        borderRadius: 4,
        border: '2px solid #E2E8F0',
        bgcolor: '#FFFFFF',
        p: { xs: 3, sm: 4 },
      }}
    >
      <Stack spacing={3}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={2}
          sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', md: 'center' } }}
        >
          <Box sx={{ flexGrow: 1, minWidth: 0 }}>
            <Typography
              variant="caption"
              sx={{
                fontWeight: 800,
                letterSpacing: 1,
                color: '#64748B',
                textTransform: 'uppercase',
              }}
            >
              {question
                ? `Question ${question.questionIndex + 1} of ${question.totalQuestions}`
                : 'Question in progress'}
            </Typography>
            <Typography
              variant="h4"
              component="h1"
              sx={{ fontWeight: 900, color: '#09131F', mt: 0.5 }}
            >
              {question ? question.text : 'End the question to continue'}
            </Typography>
          </Box>
          <AnsweredCounter answered={answeredCount} total={participantCount} />
        </Stack>

        <QuestionMedia imageUrl={question?.imageUrl} maxHeight={280} />

        <Box sx={{ maxWidth: 520, mx: 'auto', width: '100%' }}>
          <ServerCountdown {...countdown} paused={paused} size="projector" />
        </Box>

        {question ? (
          <ChoiceGrid choices={question.choices} />
        ) : (
          <Typography sx={{ textAlign: 'center', color: '#64748B', fontWeight: 600 }}>
            Reload lost the question text. Answer counts are still live — end the question to see
            results.
          </Typography>
        )}
      </Stack>
    </Card>
  );
}

export default HostQuestionView;
