import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import RadioButtonCheckedIcon from '@mui/icons-material/RadioButtonChecked';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import { Box, Stack, Typography } from '@mui/material';

import { GAME_PHASES, type GameStatus, getActiveStepIndex } from '../../constants/gameStatus.ts';

export interface GamePhaseIndicatorProps {
  status: GameStatus;
  currentQuestionIndex?: number;
  totalQuestions?: number;
}

export function GamePhaseIndicator({
  status,
  currentQuestionIndex,
  totalQuestions,
}: GamePhaseIndicatorProps) {
  const activeIndex = getActiveStepIndex(status);

  return (
    <Box
      component="nav"
      aria-label="Game phase progress"
      sx={{
        width: '100%',
        bgcolor: '#ffffff',
        py: 1.5,
        px: { xs: 2, sm: 3 },
        borderRadius: 3,
        border: '1px solid #E2E8F0',
        boxShadow: '0 2px 8px rgba(0, 0, 0, 0.04)',
      }}
    >
      <Stack
        direction="row"
        spacing={{ xs: 1, sm: 3 }}
        sx={{
          alignItems: 'center',
          justifyContent: 'space-between',
          overflowX: 'auto',
          py: 0.5,
        }}
      >
        {GAME_PHASES.map((phase, index) => {
          const isCompleted = index < activeIndex;
          const isActive = index === activeIndex;
          const isPending = index > activeIndex;

          let label = phase.label;
          if (isActive && phase.id === 'QuestionActive' && totalQuestions) {
            label = `Question ${(currentQuestionIndex ?? 0) + 1}/${totalQuestions}`;
          }

          return (
            <Stack
              key={phase.id}
              direction="row"
              spacing={1}
              sx={{
                alignItems: 'center',
                flexShrink: 0,
                opacity: isPending ? 0.45 : 1,
              }}
            >
              <Box
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  color: isActive ? '#00629B' : isCompleted ? '#10B981' : '#94A3B8',
                }}
              >
                {isCompleted && <CheckCircleIcon sx={{ fontSize: 18 }} />}
                {isActive && (
                  <RadioButtonCheckedIcon
                    sx={{
                      fontSize: 18,
                      animation: 'pulse 1.8s infinite',
                      '@keyframes pulse': {
                        '0%': { transform: 'scale(1)' },
                        '50%': { transform: 'scale(1.2)' },
                        '100%': { transform: 'scale(1)' },
                      },
                    }}
                  />
                )}
                {isPending && <RadioButtonUncheckedIcon sx={{ fontSize: 18 }} />}
              </Box>

              <Typography
                variant="body2"
                sx={{
                  fontWeight: isActive ? 800 : isCompleted ? 700 : 500,
                  color: isActive ? '#00629B' : isCompleted ? '#1E293B' : '#64748B',
                  whiteSpace: 'nowrap',
                  fontSize: { xs: '0.75rem', sm: '0.875rem' },
                }}
              >
                {label}
              </Typography>

              {index < GAME_PHASES.length - 1 && (
                <Box
                  sx={{
                    display: { xs: 'none', md: 'block' },
                    width: 24,
                    height: 2,
                    bgcolor: isCompleted ? '#10B981' : '#E2E8F0',
                    ml: 1,
                  }}
                />
              )}
            </Stack>
          );
        })}
      </Stack>
    </Box>
  );
}

export default GamePhaseIndicator;
