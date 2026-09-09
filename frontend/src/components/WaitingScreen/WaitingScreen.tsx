import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import LogoutIcon from '@mui/icons-material/Logout';
import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import VisibilityIcon from '@mui/icons-material/Visibility';
import { Box, Button, Chip, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';

import { ConfirmDialog } from '../ConfirmDialog/index.ts';
import { PlayerCountBadge } from '../PlayerCountBadge/index.ts';

export interface WaitingScreenProps {
  nickname: string;
  participantCount: number;
  onLeave: () => void;
}

const pulseKeyframes = {
  '0%': { transform: 'scale(0.75)', opacity: 0.85 },
  '70%': { transform: 'scale(1.3)', opacity: 0 },
  '100%': { transform: 'scale(0.75)', opacity: 0 },
};

export function WaitingScreen({ nickname, participantCount, onLeave }: WaitingScreenProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);

  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 4,
        border: '1px solid #E2E8F0',
        bgcolor: '#FFFFFF',
        p: { xs: 3, sm: 4 },
        textAlign: 'center',
        boxShadow: '0 20px 25px -5px rgba(9, 19, 31, 0.08), 0 8px 10px -6px rgba(9, 19, 31, 0.04)',
      }}
    >
      <Stack spacing={3} sx={{ alignItems: 'center' }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', color: '#059669' }}>
          <CheckCircleIcon />
          <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131F' }}>
            You&apos;re in!
          </Typography>
        </Stack>

        <Box
          sx={{
            position: 'relative',
            width: 168,
            height: 168,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Box
            aria-hidden
            sx={{
              position: 'absolute',
              inset: 0,
              borderRadius: '50%',
              border: '3px solid #0284C7',
              animation: 'waitingPulse 2s ease-in-out infinite',
              '@keyframes waitingPulse': pulseKeyframes,
              '@media (prefers-reduced-motion: reduce)': { animation: 'none', opacity: 0.35 },
            }}
          />
          <Box
            aria-hidden
            sx={{
              position: 'absolute',
              inset: 26,
              borderRadius: '50%',
              border: '3px solid rgba(2, 132, 199, 0.4)',
              animation: 'waitingPulse 2s ease-in-out 0.6s infinite',
              '@keyframes waitingPulse': pulseKeyframes,
              '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
            }}
          />
          <Box
            sx={{
              width: 92,
              height: 92,
              borderRadius: '50%',
              bgcolor: '#00629B',
              color: '#FFFFFF',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <SportsEsportsIcon sx={{ fontSize: 46 }} />
          </Box>
        </Box>

        <Typography variant="body1" sx={{ color: '#486581', fontWeight: 500 }}>
          Waiting for the host to start the game
        </Typography>

        <Chip
          label={nickname}
          sx={{
            height: 'auto',
            px: 1.5,
            py: 1.25,
            fontSize: '1.15rem',
            fontWeight: 800,
            bgcolor: '#EEF7FC',
            color: '#00629B',
            border: '2px solid #00629B',
            borderRadius: 3,
          }}
        />

        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', color: '#64748B' }}>
          <VisibilityIcon fontSize="small" />
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            See your name on the host&apos;s screen? You&apos;re ready to play.
          </Typography>
        </Stack>

        <Stack spacing={0.75} sx={{ alignItems: 'center' }}>
          <PlayerCountBadge count={participantCount} variant="compact" />
          <Typography variant="caption" sx={{ color: '#94A3B8', fontWeight: 600 }}>
            in the lobby
          </Typography>
        </Stack>

        <Button
          onClick={() => setConfirmOpen(true)}
          variant="outlined"
          color="inherit"
          startIcon={<LogoutIcon />}
          sx={{ minHeight: 44, fontWeight: 700, color: '#486581', borderColor: '#CBD5E1' }}
        >
          Leave Game
        </Button>
      </Stack>

      <ConfirmDialog
        open={confirmOpen}
        title="Leave this game?"
        message="You will be disconnected from the lobby. You can rejoin with the Game PIN while the host has not started the game."
        confirmLabel="Leave Game"
        confirmColor="error"
        onConfirm={() => {
          setConfirmOpen(false);
          onLeave();
        }}
        onCancel={() => setConfirmOpen(false)}
      />
    </Paper>
  );
}

export default WaitingScreen;
