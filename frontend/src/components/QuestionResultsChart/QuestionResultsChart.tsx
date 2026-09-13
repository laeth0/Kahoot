import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { Box, Stack, Typography } from '@mui/material';

import type { ChoiceResultResponse, QuestionResultsResponse } from '../../realtime/events.ts';
import { ChoiceShape } from '../ChoiceGrid/ChoiceShape.tsx';
import { choiceColor, choiceLetter } from '../ChoiceGrid/choiceVisuals.ts';

export interface QuestionResultsChartProps {
  results: QuestionResultsResponse;
}

interface ChoiceResultRowProps {
  choice: ChoiceResultResponse;
  index: number;
  maxCount: number;
}

function ChoiceResultRow({ choice, index, maxCount }: ChoiceResultRowProps) {
  const widthPercent = Math.round((choice.answerCount / maxCount) * 100);
  const label = choice.text?.trim() ? choice.text : `Answer ${choiceLetter(index)}`;

  return (
    <Box>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 0.5 }}>
        <Stack
          sx={{
            alignItems: 'center',
            justifyContent: 'center',
            width: 34,
            height: 34,
            borderRadius: 1.5,
            bgcolor: choiceColor(index),
            color: '#FFFFFF',
            flexShrink: 0,
          }}
        >
          <ChoiceShape index={index} fontSize={16} />
          <Typography sx={{ fontWeight: 900, fontSize: '0.6rem', lineHeight: 1 }}>
            {choiceLetter(index)}
          </Typography>
        </Stack>

        <Typography
          sx={{
            fontWeight: 700,
            color: '#09131F',
            flexGrow: 1,
            minWidth: 0,
            overflowWrap: 'anywhere',
          }}
        >
          {label}
        </Typography>

        {choice.isCorrect && (
          <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', color: '#059669' }}>
            <CheckCircleIcon sx={{ fontSize: 18 }} />
            <Typography sx={{ fontWeight: 800, fontSize: '0.8rem' }}>Correct</Typography>
          </Stack>
        )}

        <Typography sx={{ fontWeight: 800, color: '#09131F', minWidth: 32, textAlign: 'right' }}>
          {choice.answerCount}
        </Typography>
      </Stack>

      <Box
        sx={{
          height: 12,
          borderRadius: 999,
          bgcolor: '#EEF2F7',
          overflow: 'hidden',
        }}
      >
        <Box
          sx={{
            height: '100%',
            width: `${widthPercent}%`,
            bgcolor: choice.isCorrect ? '#10B981' : choiceColor(index),
            borderRadius: 999,
            transition: 'width 0.4s ease',
            '@media (prefers-reduced-motion: reduce)': { transition: 'none' },
          }}
        />
      </Box>
    </Box>
  );
}

export function QuestionResultsChart({ results }: QuestionResultsChartProps) {
  const maxCount = Math.max(1, ...results.choices.map((choice) => choice.answerCount));

  return (
    <Stack spacing={1.5} sx={{ width: '100%' }}>
      {results.choices.map((choice, index) => (
        <ChoiceResultRow key={choice.choiceId} choice={choice} index={index} maxCount={maxCount} />
      ))}

      <Typography sx={{ color: '#64748B', fontWeight: 600, fontSize: '0.85rem', pt: 0.5 }}>
        {results.answerCount} of {results.participantCount} players answered
      </Typography>
    </Stack>
  );
}

export default QuestionResultsChart;
