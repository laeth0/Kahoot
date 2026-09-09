import { Box } from '@mui/material';

import { ChoiceButton, type ChoiceReveal } from './ChoiceButton.tsx';

export interface ChoiceOption {
  id: string;
  text?: string | null;
  imageUrl?: string | null;
}

export interface ChoiceGridProps {
  choices: ChoiceOption[];
  selectedChoiceId?: string | null;
  disabled?: boolean;
  onSelect?: (choiceId: string) => void;
  correctChoiceId?: string | null;
  revealDistribution?: boolean;
  counts?: Record<string, number> | null;
}

export function ChoiceGrid({
  choices,
  selectedChoiceId = null,
  disabled = false,
  onSelect,
  correctChoiceId = null,
  revealDistribution = false,
  counts = null,
}: ChoiceGridProps) {
  const revealing = Boolean(correctChoiceId);

  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)' },
        gap: 1.5,
        width: '100%',
      }}
    >
      {choices.map((choice, index) => {
        let reveal: ChoiceReveal = null;
        let count: number | null = null;

        if (revealing) {
          if (choice.id === correctChoiceId) {
            reveal = 'correct';
          } else if (revealDistribution && choice.id === selectedChoiceId) {
            reveal = 'incorrect';
          } else if (revealDistribution) {
            reveal = 'muted';
          }
          if (revealDistribution) {
            count = counts?.[choice.id] ?? 0;
          }
        }

        const interactive = !revealing && !disabled && Boolean(onSelect);

        return (
          <ChoiceButton
            key={choice.id}
            index={index}
            text={choice.text}
            imageUrl={choice.imageUrl}
            selected={choice.id === selectedChoiceId}
            disabled={!interactive}
            reveal={reveal}
            count={count}
            onClick={interactive && onSelect ? () => onSelect(choice.id) : undefined}
          />
        );
      })}
    </Box>
  );
}

export default ChoiceGrid;
