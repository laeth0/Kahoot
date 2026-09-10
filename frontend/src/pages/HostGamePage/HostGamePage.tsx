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
import { ConnectionStatusBanner } from '../../components/ConnectionStatusBanner/index.ts';
import { AppErrorBoundary } from '../../components/ErrorBoundary/index.ts';
import { LiveRegion } from '../../components/Feedback/index.ts';
import { GamePhaseIndicator } from '../../components/GamePhaseIndicator/index.ts';
import { GamePinDisplay } from '../../components/GamePinDisplay/index.ts';
import { HostGameControls } from '../../components/HostGameControls/index.ts';
import { LeaderboardList } from '../../components/LeaderboardList/index.ts';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { ParticipantGrid } from '../../components/ParticipantGrid/index.ts';
import { PlayerCountBadge } from '../../components/PlayerCountBadge/index.ts';
import { PodiumView } from '../../components/PodiumView/index.ts';
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
    <Box sx={{ mt: 5 }}>
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

  const leaderboardCard = (heading: string, maxRows: number) => (
    <Card sx={{ borderRadius: 4, border: '2px solid #E2E8F0', p: { xs: 3, sm: 4 } }}>
      <Typography variant="h4" component="h1" sx={{ fontWeight: 900, color: '#09131F', mb: 3 }}>
        {heading}
      </Typography>
      <LeaderboardList entries={leaderboard?.entries ?? []} maxRows={maxRows} size="projector" />
    </Card>
  );

  return (
    <GameLayout quizTitle={gameState.quizTitle} gamePin={gameState.pin} isGameActive={!isFinished}>
      <MetadataManager title={`${gameState.quizTitle} - Host - Kahoot`} noindex />

      <Box ref={mainRegionRef} sx={{ maxWidth: 1400, mx: 'auto', pb: 8 }}>
        <LiveRegion message={liveAnnouncement?.message} politeness={liveAnnouncement?.politeness} />
        <ConnectionStatusBanner status={hubConnectionStatus} onRetry={retryHub} />

        <Box sx={{ mb: 3 }}>
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
              <GamePinDisplay pin={gameState.pin} quizTitle={gameState.quizTitle} />
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
          <>
            {leaderboardCard('Standings', 12)}
            {controls}
          </>
        )}

        {isFinished && (
          <Stack spacing={3}>
            <Card sx={{ borderRadius: 4, border: '2px solid #00629B', p: { xs: 3, sm: 4 } }}>
              <Typography
                variant="h4"
                component="h1"
                sx={{ fontWeight: 900, color: '#09131F', mb: 1, textAlign: 'center' }}
              >
                Final Results
              </Typography>
              <PodiumView entries={leaderboard?.entries ?? []} />
            </Card>

            {leaderboardCard('Full Standings', 20)}

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
                sx={{ fontWeight: 700, textTransform: 'none', minHeight: 48 }}
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
                  }}
                >
                  {isCreatingNew ? 'Starting…' : 'Start New Session'}
                </Button>
              )}
            </Stack>
          </Stack>
        )}
      </Box>
    </GameLayout>
  );
}

export default HostGamePage;
