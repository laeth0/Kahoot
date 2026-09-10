import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import CancelIcon from '@mui/icons-material/Cancel';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import StopCircleIcon from '@mui/icons-material/StopCircle';
import { Alert, Box, Button, CircularProgress, Paper, Stack, Tooltip } from '@mui/material';
import { type ReactNode, useState } from 'react';

import { type GameStatus, normalizeGameStatus } from '../../constants/gameStatus.ts';
import { ConfirmDialog } from '../ConfirmDialog/index.ts';

export interface HostGameControlsProps {
  status: GameStatus;
  participantCount: number;
  isActionPending?: boolean;
  actionError?: string | null;
  hasNextQuestion?: boolean;
  onStartGame: () => Promise<void> | void;
  onEndGame: () => Promise<void> | void;
  onEndQuestion?: () => Promise<void> | void;
  onNextQuestion?: () => Promise<void> | void;
  onShowLeaderboard?: () => Promise<void> | void;
}

const primaryButtonSx = {
  fontWeight: 800,
  minHeight: 52,
  fontSize: '1.05rem',
  borderRadius: 3,
  textTransform: 'none',
  px: { xs: 3, sm: 4.5 },
  width: { xs: '100%', sm: 'auto' },
} as const;

export function HostGameControls({
  status,
  participantCount,
  isActionPending = false,
  actionError = null,
  hasNextQuestion = true,
  onStartGame,
  onEndGame,
  onEndQuestion,
  onNextQuestion,
  onShowLeaderboard,
}: HostGameControlsProps) {
  const [showEndGameConfirm, setShowEndGameConfirm] = useState(false);

  const phase = normalizeGameStatus(status);
  const isFinished = phase === 'Finished';

  const handleConfirmEnd = async () => {
    setShowEndGameConfirm(false);
    await onEndGame();
  };

  const pendingIcon = <CircularProgress size={20} color="inherit" />;

  const renderPrimary = (): ReactNode => {
    if (phase === 'Lobby' || phase === 'Created') {
      const hasPlayers = participantCount >= 1;
      return (
        <Tooltip
          title={
            hasPlayers
              ? 'Launch Question 1 for all connected players'
              : 'At least 1 player must join before you can start.'
          }
          arrow
          placement="top"
        >
          <Box sx={{ width: { xs: '100%', sm: 'auto' } }}>
            <Button
              variant="contained"
              color="primary"
              disabled={!hasPlayers || isActionPending}
              onClick={onStartGame}
              startIcon={isActionPending ? pendingIcon : <PlayArrowIcon />}
              sx={primaryButtonSx}
            >
              {isActionPending ? 'Starting…' : 'Start Game'}
            </Button>
          </Box>
        </Tooltip>
      );
    }

    if (phase === 'QuestionActive') {
      return (
        <Button
          variant="contained"
          color="primary"
          disabled={isActionPending || !onEndQuestion}
          onClick={onEndQuestion}
          startIcon={isActionPending ? pendingIcon : <StopCircleIcon />}
          sx={primaryButtonSx}
        >
          {isActionPending ? 'Ending…' : 'End Question'}
        </Button>
      );
    }

    if (phase === 'QuestionResults' || phase === 'Leaderboard') {
      return (
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={1.5}
          sx={{ width: { xs: '100%', sm: 'auto' }, alignItems: 'center' }}
        >
          {phase === 'QuestionResults' && onShowLeaderboard && (
            <Button
              variant="outlined"
              color="primary"
              disabled={isActionPending}
              onClick={onShowLeaderboard}
              startIcon={<LeaderboardIcon />}
              sx={{
                fontWeight: 700,
                minHeight: 48,
                borderRadius: 3,
                textTransform: 'none',
                px: 3,
                width: { xs: '100%', sm: 'auto' },
              }}
            >
              Show Leaderboard
            </Button>
          )}
          <Tooltip
            title={
              hasNextQuestion
                ? ''
                : 'That was the last question — show the leaderboard or end the game.'
            }
            arrow
            placement="top"
            disableHoverListener={hasNextQuestion}
          >
            <Box sx={{ width: { xs: '100%', sm: 'auto' } }}>
              <Button
                variant="contained"
                color="primary"
                disabled={isActionPending || !onNextQuestion || !hasNextQuestion}
                onClick={onNextQuestion}
                startIcon={isActionPending ? pendingIcon : <ArrowForwardIcon />}
                sx={primaryButtonSx}
              >
                {isActionPending ? 'Loading…' : 'Next Question'}
              </Button>
            </Box>
          </Tooltip>
        </Stack>
      );
    }

    return null;
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
        {actionError && (
          <Alert severity="warning" sx={{ mb: 1.5, borderRadius: 2 }}>
            {actionError}
          </Alert>
        )}

        <Stack
          direction={{ xs: 'column-reverse', sm: 'row' }}
          spacing={2}
          sx={{ alignItems: 'center', justifyContent: 'space-between' }}
        >
          <Button
            variant="outlined"
            color="error"
            onClick={() => setShowEndGameConfirm(true)}
            disabled={isActionPending || isFinished}
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

          <Box
            sx={{ width: { xs: '100%', sm: 'auto' }, display: 'flex', justifyContent: 'flex-end' }}
          >
            {renderPrimary()}
          </Box>
        </Stack>
      </Paper>

      <ConfirmDialog
        open={showEndGameConfirm}
        title="End Live Game Session?"
        message="All connected players will be disconnected and the session will be marked as finished."
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
