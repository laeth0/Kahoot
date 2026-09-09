import { Box, LinearProgress, Stack, Typography } from '@mui/material';

import type { CountdownState } from '../../hooks/useServerCountdown.ts';

export interface ServerCountdownProps extends CountdownState {
  paused?: boolean;
  size?: 'player' | 'projector';
}

function barColor(fraction: number, expired: boolean): string {
  if (expired) {
    return '#94A3B8';
  }
  if (fraction > 0.5) {
    return '#10B981';
  }
  if (fraction > 0.25) {
    return '#F59E0B';
  }
  return '#EF4444';
}

export function ServerCountdown({
  secondsLeft,
  fraction,
  expired,
  paused = false,
  size = 'player',
}: ServerCountdownProps) {
  const isProjector = size === 'projector';
  const color = barColor(fraction, expired);

  return (
    <Box
      role="timer"
      aria-live="off"
      aria-label={
        paused
          ? 'Timer paused, reconnecting'
          : expired
            ? 'Time is up'
            : `${secondsLeft} seconds left`
      }
      sx={{ width: '100%' }}
    >
      <Stack
        direction="row"
        spacing={1}
        sx={{ alignItems: 'baseline', justifyContent: 'center', mb: 1 }}
      >
        <Typography
          sx={{
            fontWeight: 900,
            lineHeight: 1,
            color,
            fontVariantNumeric: 'tabular-nums',
            fontSize: isProjector ? { xs: '3.5rem', md: '5rem' } : '2.75rem',
          }}
        >
          {expired ? 0 : secondsLeft}
        </Typography>
        <Typography
          sx={{
            fontWeight: 700,
            color: '#64748B',
            fontSize: isProjector ? '1.25rem' : '0.95rem',
          }}
        >
          {paused ? 'paused' : 'seconds'}
        </Typography>
      </Stack>

      <LinearProgress
        variant="determinate"
        value={Math.round(fraction * 100)}
        sx={{
          height: isProjector ? 14 : 10,
          borderRadius: 999,
          bgcolor: '#E2E8F0',
          '& .MuiLinearProgress-bar': {
            bgcolor: color,
            borderRadius: 999,
            transition: 'transform 0.12s linear',
          },
          '@media (prefers-reduced-motion: reduce)': {
            '& .MuiLinearProgress-bar': { transition: 'none' },
          },
        }}
      />
    </Box>
  );
}

export default ServerCountdown;
