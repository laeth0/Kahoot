import { useState } from 'react';
import { Button, Paper, Stack, Typography } from '@mui/material';

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
  selectedChoiceIds: readonly string[];
  selectedChoiceId?: string | null;
  onSelect: (choiceIds: string[]) => void;
}

export function PlayerQuestionView({
  question,
  paused,
  answerState,
  selectedChoiceIds,
  selectedChoiceId,
  onSelect,
}: PlayerQuestionViewProps) {
  const isMultiSelect = Boolean(question.allowMultipleAnswers);
  const [pendingSelections, setPendingSelections] = useState<string[]>([]);

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

  const currentSelection = isMultiSelect
    ? selectedChoiceIds.length > 0 ? selectedChoiceIds : pendingSelections
    : selectedChoiceIds.length > 0 ? selectedChoiceIds : selectedChoiceId ? [selectedChoiceId] : [];

  const handleChoiceClick = (choiceId: string) => {
    if (locked) return;

    if (isMultiSelect) {
      setPendingSelections((prev) =>
        prev.includes(choiceId) ? prev.filter((id) => id !== choiceId) : [...prev, choiceId],
      );
    } else {
      onSelect([choiceId]);
    }
  };

  const handleSubmitMulti = () => {
    if (locked || pendingSelections.length === 0) return;
    onSelect(pendingSelections);
  };

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
          <QuestionMedia imageUrl={question.imageUrl} />
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
        <Stack spacing={2}>
          <Typography sx={{ textAlign: 'center', color: '#64748B', fontWeight: 600 }}>
            {isMultiSelect
              ? 'Multi-select: Choose all answers that apply and submit.'
              : 'Pick one answer — you cannot change it.'}
          </Typography>

          <ChoiceGrid
            choices={question.choices}
            selectedChoiceIds={currentSelection}
            disabled={locked}
            onSelect={handleChoiceClick}
          />

          {isMultiSelect && (
            <Button
              variant="contained"
              size="large"
              disabled={locked || pendingSelections.length === 0}
              onClick={handleSubmitMulti}
              sx={{
                py: 1.75,
                fontWeight: 800,
                fontSize: '1.05rem',
                borderRadius: 3,
                bgcolor: '#0ea5e9',
                boxShadow: '0 4px 14px rgba(14, 165, 233, 0.35)',
                '&:hover': { bgcolor: '#0284c7' },
                '&.Mui-disabled': { bgcolor: '#E2E8F0', color: '#94A3B8' },
              }}
            >
              Submit Answer {pendingSelections.length > 0 ? `(${pendingSelections.length} selected)` : ''}
            </Button>
          )}
        </Stack>
      )}
    </Stack>
  );
}

export default PlayerQuestionView;
