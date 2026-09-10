import SearchOffIcon from '@mui/icons-material/SearchOff';
import SportsScoreIcon from '@mui/icons-material/SportsScore';
import { Box, Button, Container, Paper, Stack, Typography } from '@mui/material';
import { type ReactNode, useEffect, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { ConnectionStatusBanner } from '../../components/ConnectionStatusBanner/index.ts';
import { AppErrorBoundary } from '../../components/ErrorBoundary/index.ts';
import { LiveRegion, LoadingState } from '../../components/Feedback/index.ts';
import { GameFinishedScreen } from '../../components/GameFinishedScreen/index.ts';
import { KickedNotice } from '../../components/KickedNotice/index.ts';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { WaitingScreen } from '../../components/WaitingScreen/index.ts';
import {
  isLeaderboardStatus,
  isLobbyStatus,
  isQuestionActiveStatus,
  isQuestionResultsStatus,
} from '../../constants/gameStatus.ts';
import { usePlayerGame } from '../../hooks/usePlayerGame.ts';
import { PlayerLeaderboardView } from './PlayerLeaderboardView.tsx';
import { PlayerQuestionView } from './PlayerQuestionView.tsx';
import { PlayerResultsView } from './PlayerResultsView.tsx';

interface NoticeCardProps {
  icon: ReactNode;
  title: string;
  body: string;
  accent?: boolean;
  actionLabel?: string;
  onAction?: () => void;
}

function NoticeCard({ icon, title, body, accent = false, actionLabel, onAction }: NoticeCardProps) {
  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 4,
        border: accent ? '1px solid #00629B' : '1px solid #E2E8F0',
        bgcolor: '#FFFFFF',
        p: { xs: 3, sm: 4 },
        textAlign: 'center',
      }}
    >
      <Stack spacing={2.5} sx={{ alignItems: 'center', maxWidth: 420, mx: 'auto' }}>
        <Box
          sx={{
            width: 64,
            height: 64,
            borderRadius: '50%',
            bgcolor: '#EEF7FC',
            color: '#00629B',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          {icon}
        </Box>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131F' }}>
          {title}
        </Typography>
        <Typography variant="body1" sx={{ color: '#486581', lineHeight: 1.6 }}>
          {body}
        </Typography>
        {actionLabel && onAction && (
          <Button
            onClick={onAction}
            variant="contained"
            color="primary"
            sx={{ minHeight: 48, px: 3, fontWeight: 700, mt: 1 }}
          >
            {actionLabel}
          </Button>
        )}
      </Stack>
    </Paper>
  );
}

export function PlayerGamePage() {
  const { gameId } = useParams<{ gameId: string }>();
  return (
    <AppErrorBoundary key={gameId ?? 'none'}>
      <PlayerGameSession key={gameId ?? 'none'} gameId={gameId} />
    </AppErrorBoundary>
  );
}

function PlayerGameSession({ gameId }: { gameId: string | undefined }) {
  const navigate = useNavigate();
  const mainRegionRef = useRef<HTMLElement | null>(null);

  const {
    playerState,
    isKicked,
    isLoading,
    error,
    hubStatus,
    retryHub,
    leaveGame,
    submitAnswer,
    answerState,
    selectedChoiceId,
    pointsThisQuestion,
    rankDelta,
    liveAnnouncement,
  } = usePlayerGame(gameId);

  const prevStatusRef = useRef(playerState?.status);

  useEffect(() => {
    if (playerState?.status && playerState.status !== prevStatusRef.current) {
      prevStatusRef.current = playerState.status;
      const heading = mainRegionRef.current?.querySelector('h1');
      if (heading instanceof HTMLElement) {
        heading.setAttribute('tabindex', '-1');
        heading.focus();
      }
    }
  }, [playerState?.status]);

  const goHome = () => navigate('/');

  const handleLeave = () => {
    void leaveGame();
    goHome();
  };

  const handlePlayAgain = () => {
    void leaveGame();
    navigate('/join');
  };

  const renderBody = (): ReactNode => {
    if (isKicked) {
      return <KickedNotice onHome={goHome} />;
    }

    if (isLoading) {
      return <LoadingState variant="page" message="Connecting to game lobby..." />;
    }

    if (error || !playerState) {
      return (
        <NoticeCard
          icon={<SearchOffIcon sx={{ fontSize: 34 }} />}
          title="Game session not found"
          body={
            error ??
            'We could not connect you to this game. It may have ended or the link is no longer valid.'
          }
          actionLabel="Join a Game"
          onAction={() => navigate('/join')}
        />
      );
    }

    if (isLobbyStatus(playerState.status)) {
      return (
        <WaitingScreen
          nickname={playerState.nickname}
          participantCount={playerState.participantCount}
          onLeave={handleLeave}
        />
      );
    }

    if (isQuestionActiveStatus(playerState.status) && playerState.currentQuestion) {
      return (
        <PlayerQuestionView
          question={playerState.currentQuestion}
          paused={hubStatus !== 'connected'}
          answerState={answerState}
          selectedChoiceId={selectedChoiceId}
          onSelect={submitAnswer}
        />
      );
    }

    if (isQuestionResultsStatus(playerState.status)) {
      return playerState.lastResults ? (
        <PlayerResultsView
          question={playerState.currentQuestion}
          results={playerState.lastResults}
          selectedChoiceId={selectedChoiceId}
          totalScore={playerState.totalScore}
          pointsThisQuestion={pointsThisQuestion}
        />
      ) : (
        <NoticeCard
          accent
          icon={<SportsScoreIcon sx={{ fontSize: 34 }} />}
          title="Results are in"
          body="Waiting for the host to reveal the scores…"
        />
      );
    }

    if (isLeaderboardStatus(playerState.status)) {
      return (
        <PlayerLeaderboardView
          participantId={playerState.participantId}
          rank={playerState.rank}
          totalScore={playerState.totalScore}
          rankDelta={rankDelta}
          leaderboard={playerState.leaderboard}
        />
      );
    }

    if (playerState.status === 'Finished') {
      return (
        <GameFinishedScreen
          nickname={playerState.nickname}
          participantId={playerState.participantId}
          rank={playerState.rank}
          totalScore={playerState.totalScore}
          entries={playerState.leaderboard?.entries ?? []}
          onPlayAgain={handlePlayAgain}
          onLeave={handleLeave}
        />
      );
    }

    return <LoadingState variant="page" message="Syncing with the game…" />;
  };

  return (
    <Box
      ref={mainRegionRef}
      component="main"
      sx={{
        minHeight: '100dvh',
        bgcolor: '#F4F8FC',
        display: 'flex',
        flexDirection: 'column',
        px: 2,
        py: { xs: 3, sm: 5 },
        paddingBottom: 'max(env(safe-area-inset-bottom), 24px)',
      }}
    >
      <MetadataManager title="Live Game - Kahoot" noindex />
      <LiveRegion message={liveAnnouncement?.message} politeness={liveAnnouncement?.politeness} />

      <Container
        maxWidth={false}
        disableGutters
        sx={{
          width: '100%',
          maxWidth: 600,
          mx: 'auto',
          flexGrow: 1,
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        {!isKicked && <ConnectionStatusBanner status={hubStatus} onRetry={retryHub} />}

        <Box
          sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center' }}
        >
          {renderBody()}
        </Box>
      </Container>
    </Box>
  );
}

export default PlayerGamePage;
