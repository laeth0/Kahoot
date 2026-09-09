import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Typography,
} from '@mui/material';
import { useNavigate, useParams } from 'react-router-dom';

import { ConnectionStatusBanner } from '../../components/ConnectionStatusBanner/index.ts';
import { GamePhaseIndicator } from '../../components/GamePhaseIndicator/index.ts';
import { GamePinDisplay } from '../../components/GamePinDisplay/index.ts';
import { HostGameControls } from '../../components/HostGameControls/index.ts';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { ParticipantGrid } from '../../components/ParticipantGrid/index.ts';
import { PlayerCountBadge } from '../../components/PlayerCountBadge/index.ts';
import { useHostGame } from '../../hooks/useHostGame.ts';
import { GameLayout } from '../../layouts/GameLayout.tsx';

export function HostGamePage() {
  const { gameId } = useParams<{ gameId: string }>();
  const navigate = useNavigate();

  const {
    gameState,
    participants,
    participantCount,
    isLoading,
    error,
    isActionPending,
    hubConnectionStatus,
    retryHub,
    refetch,
    startGame,
    endGame,
    removeParticipant,
  } = useHostGame(gameId);

  const pageTitle = gameState?.quizTitle
    ? `${gameState.quizTitle} - Host Lobby`
    : 'Host Game Lobby';

  if (isLoading) {
    return (
      <GameLayout>
        <MetadataManager title="Loading Live Session... - Kahoot" noIndex />
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            minHeight: '60vh',
            textAlign: 'center',
          }}
        >
          <CircularProgress size={48} sx={{ color: '#00629B', mb: 3 }} />
          <Typography variant="h6" sx={{ fontWeight: 700, color: '#09131F', mb: 1 }}>
            Connecting to Live Game Session...
          </Typography>
          <Typography variant="body2" sx={{ color: '#64748B' }}>
            Preparing the projector lobby and establishing real-time connection.
          </Typography>
        </Box>
      </GameLayout>
    );
  }

  if (error || !gameState) {
    return (
      <GameLayout>
        <MetadataManager title="Game Session Error - Kahoot" noIndex />
        <Box sx={{ maxWidth: 560, mx: 'auto', mt: 8 }}>
          <Card
            sx={{
              p: 3,
              borderRadius: 4,
              border: '1px solid #FECACA',
              boxShadow: '0 8px 24px rgba(220, 38, 38, 0.08)',
            }}
          >
            <CardContent sx={{ textAlign: 'center' }}>
              <Alert severity="error" sx={{ mb: 3, borderRadius: 2 }}>
                {error || 'Unable to find or load this game session.'}
              </Alert>
              <Typography variant="body1" sx={{ color: '#475569', mb: 3 }}>
                The game session may have expired, or you do not have permission to host this game.
              </Typography>
              <Stack direction="row" spacing={2} sx={{ justifyContent: 'center' }}>
                <Button
                  variant="outlined"
                  startIcon={<RefreshIcon />}
                  onClick={refetch}
                  sx={{ fontWeight: 700, textTransform: 'none' }}
                >
                  Try Again
                </Button>
                <Button
                  variant="contained"
                  startIcon={<ArrowBackIcon />}
                  onClick={() => navigate('/host/quizzes')}
                  sx={{
                    fontWeight: 700,
                    textTransform: 'none',
                    bgcolor: '#00629B',
                    '&:hover': { bgcolor: '#004f7d' },
                  }}
                >
                  Back to Quizzes
                </Button>
              </Stack>
            </CardContent>
          </Card>
        </Box>
      </GameLayout>
    );
  }

  const isLobby = gameState.status === 'Lobby' || gameState.status === 'Created';
  const isFinished = gameState.status === 'Finished';

  return (
    <GameLayout
      quizTitle={gameState.quizTitle}
      gamePin={gameState.pin}
      isGameActive={!isFinished}
    >
      <MetadataManager title={`${pageTitle} - Kahoot`} noIndex />

      <Box sx={{ maxWidth: 1400, mx: 'auto', pb: 8 }}>
        <ConnectionStatusBanner status={hubConnectionStatus} onRetry={retryHub} />

        <Box sx={{ mb: 3 }}>
          <GamePhaseIndicator
            status={gameState.status}
            currentQuestionIndex={gameState.currentQuestionIndex}
            totalQuestions={gameState.totalQuestions}
          />
        </Box>

        {isFinished && (
          <Box sx={{ mb: 4 }}>
            <Alert
              severity="info"
              action={
                <Button
                  color="inherit"
                  size="small"
                  onClick={() => navigate('/host/quizzes')}
                  sx={{ fontWeight: 700 }}
                >
                  Return to Quizzes
                </Button>
              }
              sx={{ borderRadius: 3, fontWeight: 600 }}
            >
              This game session has finished. You can start a new session from your Quiz Library.
            </Alert>
          </Box>
        )}

        {isLobby && (
          <>
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', lg: '3fr 1fr' },
                gap: 3,
                alignItems: 'stretch',
                mb: 4,
              }}
            >
              <GamePinDisplay pin={gameState.pin} quizTitle={gameState.quizTitle} />

              <Card
                sx={{
                  borderRadius: 4,
                  border: '2px solid #E2E8F0',
                  bgcolor: '#ffffff',
                  boxShadow: '0 8px 24px rgba(0, 0, 0, 0.04)',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'center',
                  alignItems: 'center',
                  p: { xs: 3, sm: 4 },
                  textAlign: 'center',
                }}
              >
                <PlayerCountBadge count={participantCount} variant="projector" />
                <Typography
                  variant="body2"
                  sx={{ color: '#64748B', mt: 2, fontWeight: 500, maxWidth: 220 }}
                >
                  {participantCount === 0
                    ? 'Waiting for players to connect...'
                    : 'Players ready! You can start the game whenever you are ready.'}
                </Typography>
              </Card>
            </Box>

            <ParticipantGrid
              participants={participants}
              onRemoveParticipant={removeParticipant}
              isRemoving={isActionPending}
            />

            <Box sx={{ mt: 5 }}>
              <HostGameControls
                status={gameState.status}
                participantCount={participantCount}
                isActionPending={isActionPending}
                onStartGame={startGame}
                onEndGame={endGame}
              />
            </Box>
          </>
        )}

        {!isLobby && !isFinished && (
          <Card
            sx={{
              p: 5,
              textAlign: 'center',
              borderRadius: 4,
              border: '2px solid #00629B',
              bgcolor: '#ffffff',
            }}
          >
            <Typography variant="h5" sx={{ fontWeight: 800, color: '#09131F', mb: 2 }}>
              Game in Progress: {gameState.status}
            </Typography>
            <Typography variant="body1" sx={{ color: '#64748B', mb: 4 }}>
              Question {(gameState.currentQuestionIndex ?? 0) + 1} of {gameState.totalQuestions}
            </Typography>
            <HostGameControls
              status={gameState.status}
              participantCount={participantCount}
              isActionPending={isActionPending}
              onStartGame={startGame}
              onEndGame={endGame}
            />
          </Card>
        )}
      </Box>
    </GameLayout>
  );
}

export default HostGamePage;
