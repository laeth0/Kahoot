import CancelIcon from '@mui/icons-material/Cancel';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import {
  Box,
  Button,
  CircularProgress,
  Paper,
  Stack,
  Tooltip,
} from '@mui/material';
import { useState } from 'react';

import type { GameStatus } from '../../constants/gameStatus.ts';
import { ConfirmDialog } from '../ConfirmDialog/index.ts';

export interface HostGameControlsProps {
  status: GameStatus;
  participantCount: number;
  isActionPending?: boolean;
  onStartGame: () => Promise<void> | void;
  onEndGame: () => Promise<void> | void;
}

export function HostGameControls({
  status,
  participantCount,
  isActionPending = false,
  onStartGame,
  onEndGame,
}: HostGameControlsProps) {
  const [showEndGameConfirm, setShowEndGameConfirm] = useState(false);

  const canStart = status === 'Lobby' || status === 'Created';
  const hasEnoughPlayers = participantCount >= 1;
  const isStartDisabled = !canStart || !hasEnoughPlayers || isActionPending;

  const handleConfirmEnd = async () => {
    setShowEndGameConfirm(false);
    await onEndGame();
  };

  return (
    <>
      <Paper
        elevation={4}
        sx={{
          position: 'sticky',
          bottom: 24,
          zIndex: 10,
          p: 2,
          px: { xs: 2.5, sm: 4 },
          borderRadius: 4,
          bgcolor: '#ffffff',
          border: '2px solid #E2E8F0',
          boxShadow: '0 12px 32px rgba(0, 0, 0, 0.12)',
        }}
      >
        <Stack
          direction={{ xs: 'column-reverse', sm: 'row' }}
          spacing={2}
          sx={{
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <Button
            variant="outlined"
            color="error"
            onClick={() => setShowEndGameConfirm(true)}
            disabled={isActionPending || status === 'Finished'}
            startIcon={<CancelIcon />}
            sx={{
              fontWeight: 700,
              minHeight: 48,
              borderRadius: 3,
              textTransform: 'none',
              px: 3,
              width: { xs: '100%', sm: 'auto' },
            }}
          >
            End Game
          </Button>

          <Stack
            direction="row"
            spacing={2}
            sx={{
              alignItems: 'center',
              width: { xs: '100%', sm: 'auto' },
              justifyContent: 'flex-end',
            }}
          >
            {canStart && (
              <Tooltip
                title={
                  !hasEnoughPlayers
                    ? 'At least 1 player must join before you can start the game.'
                    : 'Launch Question 1 for all connected players'
                }
                arrow
                placement="top"
              >
                <Box sx={{ width: { xs: '100%', sm: 'auto' } }}>
                  <Button
                    variant="contained"
                    color="primary"
                    disabled={isStartDisabled}
                    onClick={onStartGame}
                    startIcon={
                      isActionPending ? (
                        <CircularProgress size={20} color="inherit" />
                      ) : (
                        <PlayArrowIcon />
                      )
                    }
                    sx={{
                      fontWeight: 800,
                      minHeight: 52,
                      fontSize: '1.1rem',
                      borderRadius: 3,
                      textTransform: 'none',
                      px: { xs: 3, sm: 5 },
                      width: { xs: '100%', sm: 'auto' },
                      bgcolor: hasEnoughPlayers ? '#00629B' : undefined,
                      boxShadow: hasEnoughPlayers
                        ? '0 8px 20px rgba(0, 98, 155, 0.35)'
                        : 'none',
                      '&:hover': {
                        bgcolor: '#004F7D',
                      },
                    }}
                  >
                    {isActionPending ? 'Starting...' : 'Start Game'}
                  </Button>
                </Box>
              </Tooltip>
            )}
          </Stack>
        </Stack>
      </Paper>

      <ConfirmDialog
        open={showEndGameConfirm}
        title="End Live Game Session?"
        message="Are you sure you want to end this game? All connected players will be disconnected and the session will be marked as finished."
        confirmLabel="End Game"
        confirmColor="error"
        isConfirming={isActionPending}
        onConfirm={handleConfirmEnd}
        onCancel={() => setShowEndGameConfirm(false)}
      />
    </>
  );
}

export default HostGameControls;
