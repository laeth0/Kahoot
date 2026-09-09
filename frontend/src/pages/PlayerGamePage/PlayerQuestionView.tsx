import { Paper, Stack, Typography } from '@mui/material';

import { AnswerFeedbackScreen } from '../../components/AnswerFeedbackScreen/index.ts';
import { ChoiceGrid } from '../../components/ChoiceGrid/index.ts';
import { QuestionMedia } from '../../components/QuestionMedia/index.ts';
import { ServerCountdown } from '../../components/ServerCountdown/index.ts';
import type { AnswerState } from '../../hooks/usePlayerGame.ts';
import { useServerCountdown } from '../../hooks/useServerCountdown.ts';
import type { PlayerQuestionResponse } from '../../realtime/events.ts';

export interface PlayerQuestionViewProps {
  question: PlayerQuestionResponse;
  paused: boolean;
  answerState: AnswerState;
  selectedChoiceId: string | null;
  onSelect: (choiceId: string) => void;
}

export function PlayerQuestionView({
  question,
  paused,
  answerState,
  selectedChoiceId,
  onSelect,
}: PlayerQuestionViewProps) {
  const countdown = useServerCountdown(question.endsAt, {
    totalSeconds: question.timeLimitSeconds,
    paused,
  });

  if (
    answerState === 'accepted' ||
    answerState === 'alreadyAnswered' ||
    answerState === 'tooLate'
  ) {
    return <AnswerFeedbackScreen variant={answerState} />;
  }

  const locked = answerState === 'submitting' || countdown.expired || paused;
  const timeUp = countdown.expired;

  return (
    <Stack spacing={2.5}>
      <Paper
        elevation={0}
        sx={{
          borderRadius: 4,
          border: '1px solid #E2E8F0',
          bgcolor: '#FFFFFF',
          p: { xs: 2.5, sm: 3.5 },
        }}
      >
        <Stack spacing={2}>
          <Typography
            variant="caption"
            sx={{ fontWeight: 800, letterSpacing: 1, color: '#64748B', textTransform: 'uppercase' }}
          >
            Question {question.questionIndex + 1} of {question.totalQuestions}
          </Typography>
          <Typography variant="h5" component="h1" sx={{ fontWeight: 800, color: '#09131F' }}>
            {question.text}
          </Typography>
          <QuestionMedia imageUrl={question.imageUrl} maxHeight={220} />
        </Stack>
      </Paper>

      <ServerCountdown {...countdown} paused={paused} size="player" />

      {(answerState === 'rejected' || answerState === 'slowDown') && (
        <AnswerFeedbackScreen variant={answerState} />
      )}

      {timeUp && answerState === 'idle' ? (
        <Typography sx={{ textAlign: 'center', color: '#64748B', fontWeight: 700, py: 2 }}>
          Time is up — waiting for the results…
        </Typography>
      ) : (
        <Stack spacing={1.5}>
          <Typography sx={{ textAlign: 'center', color: '#64748B', fontWeight: 600 }}>
            Pick one answer — you cannot change it.
          </Typography>
          <ChoiceGrid
            choices={question.choices}
            selectedChoiceId={selectedChoiceId}
            disabled={locked}
            onSelect={onSelect}
          />
        </Stack>
      )}
    </Stack>
  );
}

export default PlayerQuestionView;
