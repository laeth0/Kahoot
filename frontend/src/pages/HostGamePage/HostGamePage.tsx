import AddIcon from '@mui/icons-material/Add';
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
import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';

import { hostGameService } from '../../api/hostGameService.ts';
import { CelebrationOverlay } from '../../components/CelebrationOverlay/index.ts';
import { ConnectionStatusBanner } from '../../components/ConnectionStatusBanner/index.ts';
import { AppErrorBoundary } from '../../components/ErrorBoundary/index.ts';
import { LiveRegion } from '../../components/Feedback/index.ts';
import { GamePhaseIndicator } from '../../components/GamePhaseIndicator/index.ts';
import { GamePinDisplay } from '../../components/GamePinDisplay/index.ts';
import { HostGameControls } from '../../components/HostGameControls/index.ts';
import { LeaderboardCard } from '../../components/LeaderboardCard/index.ts';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { ParticipantGrid } from '../../components/ParticipantGrid/index.ts';
import { PlayerCountBadge } from '../../components/PlayerCountBadge/index.ts';
import { QuestionResultsChart } from '../../components/QuestionResultsChart/index.ts';
import {
  isFinishedStatus,
  isLeaderboardStatus,
  isLobbyStatus,
  isQuestionActiveStatus,
  isQuestionResultsStatus,
} from '../../constants/gameStatus.ts';
import { useHostGame } from '../../hooks/useHostGame.ts';
import { GameLayout } from '../../layouts/GameLayout.tsx';
import { HostQuestionView } from './HostQuestionView.tsx';

export function HostGamePage() {
  const { gameId } = useParams<{ gameId: string }>();
  return (
    <AppErrorBoundary key={gameId ?? 'none'}>
      <HostGameSession key={gameId ?? 'none'} gameId={gameId} />
    </AppErrorBoundary>
  );
}

function HostGameSession({ gameId }: { gameId: string | undefined }) {
  const navigate = useNavigate();
  const location = useLocation();
  const quizId = (location.state as { quizId?: string } | null)?.quizId ?? null;
  const mainRegionRef = useRef<HTMLElement | null>(null);

  const [isCreatingNew, setIsCreatingNew] = useState(false);
  const [newSessionError, setNewSessionError] = useState<string | null>(null);

  const {
    gameState,
    participants,
    participantCount,
    currentQuestion,
    questionResults,
    leaderboard,
    isLoading,
    error,
    actionError,
    isActionPending,
    hubConnectionStatus,
    retryHub,
    refetch,
    startGame,
    advanceQuestion,
    endQuestion,
    showLeaderboard,
    endGame,
    removeParticipant,
    liveAnnouncement,
  } = useHostGame(gameId);

  const prevStatusRef = useRef(gameState?.status);

  useEffect(() => {
    if (gameState?.status && gameState.status !== prevStatusRef.current) {
      prevStatusRef.current = gameState.status;
      const heading = mainRegionRef.current?.querySelector('h1');
      if (heading instanceof HTMLElement) {
        heading.setAttribute('tabindex', '-1');
        heading.focus();
      }
    }
  }, [gameState?.status]);

  const handleStartNewSession = async () => {
    if (!quizId || isCreatingNew) {
      return;
    }
    setIsCreatingNew(true);
    setNewSessionError(null);
    try {
      const created = await hostGameService.createGame(quizId);
      navigate(`/host/game/${created.gameId}`, { state: { quizId }, replace: true });
    } catch (err) {
      setNewSessionError(err instanceof Error ? err.message : 'Could not start a new session.');
    } finally {
      setIsCreatingNew(false);
    }
  };

  if (isLoading) {
    return (
      <GameLayout>
        <MetadataManager title="Loading Live Session - Kahoot" noindex />
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
          <Typography variant="h6" sx={{ fontWeight: 700, color: '#09131F' }}>
            Connecting to the live game session…
          </Typography>
        </Box>
      </GameLayout>
    );
  }

  if (error || !gameState) {
    return (
      <GameLayout>
        <MetadataManager title="Game Session Error - Kahoot" noindex />
        <Box sx={{ maxWidth: 560, mx: 'auto', mt: 8 }}>
          <Card sx={{ p: 3, borderRadius: 4, border: '1px solid #FECACA' }}>
            <CardContent sx={{ textAlign: 'center' }}>
              <Alert severity="error" sx={{ mb: 3, borderRadius: 2 }}>
                {error || 'Unable to find or load this game session.'}
              </Alert>
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
                  sx={{ fontWeight: 700, textTransform: 'none', bgcolor: '#00629B' }}
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

  const status = gameState.status;
  const isFinished = isFinishedStatus(status);
  const paused = hubConnectionStatus !== 'connected';
  const hasNextQuestion =
    gameState.currentQuestionIndex == null ||
    gameState.currentQuestionIndex < gameState.totalQuestions - 1;

  const controls = (
    <Box sx={{ mt: { xs: 2, sm: 'clamp(12px, 2vh, 24px)' } }}>
      <HostGameControls
        status={status}
        participantCount={participantCount}
        isActionPending={isActionPending}
        actionError={actionError}
        hasNextQuestion={hasNextQuestion}
        onStartGame={startGame}
        onEndGame={endGame}
        onEndQuestion={endQuestion}
        onNextQuestion={advanceQuestion}
        onShowLeaderboard={showLeaderboard}
      />
    </Box>
  );

  return (
    <GameLayout quizTitle={gameState.quizTitle} gamePin={gameState.pin} isGameActive={!isFinished}>
      <MetadataManager title={`${gameState.quizTitle} - Host - Kahoot`} noindex />

      <Box
        ref={mainRegionRef}
        sx={{ maxWidth: 1400, mx: 'auto', pb: { xs: 2, sm: 'clamp(16px, 2.5vh, 32px)' } }}
      >
        <LiveRegion message={liveAnnouncement?.message} politeness={liveAnnouncement?.politeness} />
        <ConnectionStatusBanner status={hubConnectionStatus} onRetry={retryHub} />

        <Box sx={{ mb: { xs: 1.5, sm: 'clamp(10px, 1.5vh, 20px)' } }}>
          <GamePhaseIndicator
            status={status}
            currentQuestionIndex={gameState.currentQuestionIndex ?? undefined}
            totalQuestions={gameState.totalQuestions}
          />
        </Box>

        {isLobbyStatus(status) && (
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
              <GamePinDisplay
                pin={gameState.pin}
                quizTitle={gameState.quizTitle}
                joinUrl={gameState.joinUrl}
              />
              <Card
                sx={{
                  borderRadius: 4,
                  border: '2px solid #E2E8F0',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'center',
                  alignItems: 'center',
                  p: { xs: 3, sm: 4 },
                  textAlign: 'center',
                }}
              >
                <PlayerCountBadge count={participantCount} variant="projector" />
                <Typography variant="body2" sx={{ color: '#64748B', mt: 2, fontWeight: 500 }}>
                  {participantCount === 0
                    ? 'Waiting for players to connect…'
                    : 'Players ready — start whenever you are.'}
                </Typography>
              </Card>
            </Box>

            <ParticipantGrid
              participants={participants}
              onRemoveParticipant={removeParticipant}
              isRemoving={isActionPending}
            />
            {controls}
          </>
        )}

        {isQuestionActiveStatus(status) && (
          <>
            <HostQuestionView
              question={currentQuestion}
              endsAt={gameState.currentQuestionEndsAt}
              startedAt={gameState.currentQuestionStartedAt}
              answeredCount={gameState.answeredCount}
              participantCount={participantCount}
              paused={paused}
            />
            {controls}
          </>
        )}

        {isQuestionResultsStatus(status) && (
          <>
            <Card sx={{ borderRadius: 4, border: '2px solid #E2E8F0', p: { xs: 3, sm: 4 } }}>
              <Typography
                variant="h4"
                component="h1"
                sx={{ fontWeight: 900, color: '#09131F', mb: 3 }}
              >
                Question Results
              </Typography>
              {questionResults ? (
                <QuestionResultsChart results={questionResults} />
              ) : (
                <Typography sx={{ color: '#64748B', fontWeight: 600 }}>
                  Loading the answer breakdown…
                </Typography>
              )}
            </Card>
            {controls}
          </>
        )}

        {isLeaderboardStatus(status) && (
          <Box sx={{ position: 'relative', width: '100%', overflow: 'visible' }}>
            <CelebrationOverlay
              variant="leaderboard"
              triggerKey={`host_lb_${gameState.currentQuestionIndex ?? 0}`}
            />

            <Box
              sx={{
                position: 'relative',
                py: { xs: 1, sm: 'clamp(8px, 1.5vh, 20px)' },
                borderRadius: 6,
                background:
                  'radial-gradient(ellipse at 50% 25%, rgba(254, 240, 138, 0.3) 0%, rgba(186, 230, 253, 0.22) 40%, rgba(255, 255, 255, 0) 72%)',
              }}
            >
              <Typography
                aria-hidden="true"
                sx={{
                  display: { xs: 'none', xl: 'block' },
                  position: 'absolute',
                  left: { xl: 16, xxl: 40 },
                  top: '35%',
                  transform: 'rotate(-10deg)',
                  fontFamily: 'cursive, "Brush Script MT", "Segoe Print", sans-serif',
                  fontWeight: 900,
                  fontSize: { xl: '2.5rem' },
                  color: '#38BDF8',
                  letterSpacing: '0.04em',
                  textShadow: '0 4px 14px rgba(56, 189, 248, 0.25)',
                  pointerEvents: 'none',
                  userSelect: 'none',
                }}
              >
                Amazing
                <br />
                Players!
              </Typography>

              <Typography
                aria-hidden="true"
                sx={{
                  display: { xs: 'none', xl: 'block' },
                  position: 'absolute',
                  right: { xl: 16, xxl: 40 },
                  top: '48%',
                  transform: 'rotate(10deg)',
                  fontFamily: 'cursive, "Brush Script MT", "Segoe Print", sans-serif',
                  fontWeight: 900,
                  fontSize: { xl: '2.5rem' },
                  color: '#A855F7',
                  letterSpacing: '0.04em',
                  textShadow: '0 4px 14px rgba(168, 85, 247, 0.25)',
                  pointerEvents: 'none',
                  userSelect: 'none',
                }}
              >
                You're
                <br />
                Awesome!
              </Typography>

              <LeaderboardCard
                entries={leaderboard?.entries ?? []}
                size="projector"
                maxStandingsRows={10}
              />
            </Box>

            {controls}
          </Box>
        )}

        {isFinished && (
          <Box sx={{ position: 'relative', width: '100%' }}>
            <CelebrationOverlay variant="final-results" triggerKey="host_finished" />

            <Stack spacing={4}>
              <Box
                sx={{
                  position: 'relative',
                  py: { xs: 2, sm: 3 },
                  borderRadius: 6,
                  background:
                    'radial-gradient(ellipse at 50% 30%, rgba(254, 240, 138, 0.4) 0%, rgba(186, 230, 253, 0.25) 45%, rgba(255, 255, 255, 0) 75%)',
                }}
              >
                <LeaderboardCard
                  entries={leaderboard?.entries ?? []}
                  size="projector"
                  title="Final Results & Champions"
                  subtitle="Congratulations to all participants for an incredible competition!"
                  maxStandingsRows={20}
                />
              </Box>

              {newSessionError && (
                <Alert severity="error" sx={{ borderRadius: 2 }}>
                  {newSessionError}
                </Alert>
              )}

              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={2}
                sx={{ justifyContent: 'center' }}
              >
                <Button
                  variant="outlined"
                  startIcon={<ArrowBackIcon />}
                  onClick={() => navigate(quizId ? `/host/quizzes/${quizId}` : '/host/quizzes')}
                  sx={{
                    fontWeight: 700,
                    textTransform: 'none',
                    minHeight: 48,
                    borderRadius: 8,
                    px: 3.5,
                  }}
                >
                  Back to Quiz
                </Button>
                {quizId && (
                  <Button
                    variant="contained"
                    startIcon={
                      isCreatingNew ? <CircularProgress size={18} color="inherit" /> : <AddIcon />
                    }
                    onClick={handleStartNewSession}
                    disabled={isCreatingNew}
                    sx={{
                      fontWeight: 700,
                      textTransform: 'none',
                      bgcolor: '#00629B',
                      minHeight: 48,
                      borderRadius: 8,
                      px: 4,
                    }}
                  >
                    {isCreatingNew ? 'Starting…' : 'Start New Session'}
                  </Button>
                )}
              </Stack>
            </Stack>
          </Box>
        )}
      </Box>
    </GameLayout>
  );
}

export default HostGamePage;
