import { Box, CircularProgress, Stack, Typography } from '@mui/material';

export interface AnsweredCounterProps {
  answered: number;
  total: number;
  size?: 'compact' | 'projector';
}

export function AnsweredCounter({ answered, total, size = 'projector' }: AnsweredCounterProps) {
  const safeTotal = Math.max(0, total);
  const safeAnswered = Math.min(Math.max(0, answered), safeTotal || answered);
  const percent = safeTotal > 0 ? Math.round((safeAnswered / safeTotal) * 100) : 0;
  const diameter = size === 'projector' ? 96 : 64;

  return (
    <Stack
      direction="row"
      spacing={2}
      sx={{ alignItems: 'center' }}
      role="status"
      aria-label={`${safeAnswered} of ${safeTotal} players answered`}
    >
      <Box sx={{ position: 'relative', display: 'inline-flex' }}>
        <CircularProgress
          variant="determinate"
          value={100}
          size={diameter}
          thickness={4}
          sx={{ color: '#E2E8F0' }}
        />
        <CircularProgress
          variant="determinate"
          value={percent}
          size={diameter}
          thickness={4}
          sx={{
            color: '#00629B',
            position: 'absolute',
            left: 0,
            '& .MuiCircularProgress-circle': { strokeLinecap: 'round' },
          }}
        />
        <Box
          sx={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Typography
            sx={{
              fontWeight: 900,
              color: '#09131F',
              fontSize: size === 'projector' ? '1.4rem' : '1rem',
            }}
          >
            {safeAnswered}
          </Typography>
        </Box>
      </Box>
      <Box>
        <Typography
          sx={{
            fontWeight: 700,
            textTransform: 'uppercase',
            letterSpacing: 1,
            color: '#486581',
            fontSize: size === 'projector' ? '0.85rem' : '0.7rem',
          }}
        >
          Answered
        </Typography>
        <Typography
          sx={{
            fontWeight: 800,
            color: '#09131F',
            fontSize: size === 'projector' ? '1.5rem' : '1.1rem',
            lineHeight: 1.2,
          }}
        >
          {safeAnswered} / {safeTotal}
        </Typography>
      </Box>
    </Stack>
  );
}

export default AnsweredCounter;
