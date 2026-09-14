import { Box } from '@mui/material';

import { ChoiceButton, type ChoiceReveal } from './ChoiceButton.tsx';

export interface ChoiceOption {
  id: string;
  text?: string | null;
}

export interface ChoiceGridProps {
  choices: ChoiceOption[];
  selectedChoiceId?: string | null;
  selectedChoiceIds?: readonly string[] | null;
  disabled?: boolean;
  onSelect?: (choiceId: string) => void;
  correctChoiceIds?: readonly string[] | null;
  revealDistribution?: boolean;
  counts?: Record<string, number> | null;
}

export function ChoiceGrid({
  choices,
  selectedChoiceId = null,
  selectedChoiceIds = null,
  disabled = false,
  onSelect,
  correctChoiceIds = null,
  revealDistribution = false,
  counts = null,
}: ChoiceGridProps) {
  const revealing = Boolean(correctChoiceIds && correctChoiceIds.length > 0);
  const isSelected = (id: string) =>
    selectedChoiceIds ? selectedChoiceIds.includes(id) : id === selectedChoiceId;

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
          if (correctChoiceIds?.includes(choice.id)) {
            reveal = 'correct';
          } else if (revealDistribution && isSelected(choice.id)) {
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
            selected={isSelected(choice.id)}
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
