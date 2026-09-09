import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HighlightOffIcon from '@mui/icons-material/HighlightOff';
import { Box, Paper, Stack, Typography } from '@mui/material';

import { ChoiceGrid } from '../../components/ChoiceGrid/index.ts';
import type { PlayerQuestionResponse, QuestionResultsResponse } from '../../realtime/events.ts';

export interface PlayerResultsViewProps {
  question: PlayerQuestionResponse | null;
  results: QuestionResultsResponse;
  selectedChoiceId: string | null;
  totalScore: number;
  pointsThisQuestion: number | null;
}

export function PlayerResultsView({
  question,
  results,
  selectedChoiceId,
  totalScore,
  pointsThisQuestion,
}: PlayerResultsViewProps) {
  const answered = selectedChoiceId !== null;
  const correct = answered && selectedChoiceId === results.correctChoiceId;

  const counts: Record<string, number> = {};
  results.choices.forEach((choice) => {
    counts[choice.choiceId] = choice.answerCount;
  });

  const headline = !answered ? 'No answer this time' : correct ? 'Correct!' : 'Not this time';
  const tint = correct ? '#059669' : answered ? '#B91C1C' : '#64748B';

  return (
    <Stack spacing={2.5}>
      <Paper
        elevation={0}
        sx={{
          borderRadius: 4,
          border: '1px solid #E2E8F0',
          bgcolor: '#FFFFFF',
          p: { xs: 3, sm: 4 },
          textAlign: 'center',
        }}
      >
        <Stack spacing={1.5} sx={{ alignItems: 'center' }}>
          <Box sx={{ color: tint }}>
            {correct ? (
              <CheckCircleIcon sx={{ fontSize: 44 }} />
            ) : (
              <HighlightOffIcon sx={{ fontSize: 44 }} />
            )}
          </Box>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131F' }}>
            {headline}
          </Typography>
          {pointsThisQuestion !== null && pointsThisQuestion > 0 && (
            <Typography
              sx={{
                fontWeight: 900,
                color: '#059669',
                fontSize: '1.75rem',
                animation: 'pointsPop 0.4s ease-out',
                '@keyframes pointsPop': {
                  '0%': { transform: 'scale(0.6)', opacity: 0 },
                  '100%': { transform: 'scale(1)', opacity: 1 },
                },
                '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
              }}
            >
              +{pointsThisQuestion}
            </Typography>
          )}
          <Typography sx={{ color: '#486581', fontWeight: 700 }}>
            Total score: {totalScore}
          </Typography>
        </Stack>
      </Paper>

      {question && question.choices.length > 0 && (
        <ChoiceGrid
          choices={question.choices}
          correctChoiceId={results.correctChoiceId}
          revealDistribution
          counts={counts}
          selectedChoiceId={selectedChoiceId}
        />
      )}
    </Stack>
  );
}

export default PlayerResultsView;
