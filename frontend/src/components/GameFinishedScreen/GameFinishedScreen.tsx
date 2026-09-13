import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import LogoutIcon from '@mui/icons-material/Logout';
import ReplayIcon from '@mui/icons-material/Replay';
import { Box, Button, Paper, Stack, Typography } from '@mui/material';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';
import { ordinal } from '../../utils/rank.ts';
import { CelebrationOverlay } from '../CelebrationOverlay/index.ts';
import { LeaderboardList } from '../LeaderboardList/index.ts';
import { LeaderboardPodium } from '../LeaderboardPodium/index.ts';

export interface GameFinishedScreenProps {
  nickname: string;
  participantId: string;
  rank: number | null;
  totalScore: number;
  entries: LeaderboardEntryResponse[];
  onPlayAgain: () => void;
  onLeave: () => void;
}

export function GameFinishedScreen({
  nickname,
  participantId,
  rank,
  totalScore,
  entries,
  onPlayAgain,
  onLeave,
}: GameFinishedScreenProps) {
  const getWinnerBanner = () => {
    if (rank === 1) {
      return {
        badge: '🏆 CHAMPION',
        title: 'You Won The Game!',
        subtitle: 'Unstoppable! You claimed 1st place on the podium!',
        accent: '#F59E0B',
      };
    }
    if (rank === 2) {
      return {
        badge: '🥈 2ND PLACE',
        title: 'Podium Finish!',
        subtitle: 'Remarkable job securing second place in this match!',
        accent: '#64748B',
      };
    }
    if (rank === 3) {
      return {
        badge: '🥉 3RD PLACE',
        title: 'Podium Finish!',
        subtitle: 'Awesome effort claiming a spot in the top three!',
        accent: '#EA580C',
      };
    }
    return {
      badge: '🎉 GAME COMPLETED',
      title: rank ? `You Finished ${ordinal(rank)}!` : 'That is a Wrap!',
      subtitle: 'Thank you for playing and giving it your all!',
      accent: '#00629B',
    };
  };

  const banner = getWinnerBanner();

  return (
    <>
      <CelebrationOverlay
        variant="final-results"
        triggerKey={`finished_${participantId}_${totalScore}`}
      />

      <Paper
        elevation={0}
        sx={{
          position: 'relative',
          borderRadius: 4,
          border: rank === 1 ? '2px solid #F59E0B' : '1px solid #E2E8F0',
          bgcolor: '#FFFFFF',
          p: { xs: 3, sm: 4 },
          boxShadow:
            rank === 1 ? '0 8px 30px rgba(245, 158, 11, 0.2)' : '0 4px 20px rgba(0, 98, 155, 0.08)',
        }}
      >
        <Stack spacing={3.5} sx={{ alignItems: 'center' }}>
          <Stack spacing={1} sx={{ alignItems: 'center', textAlign: 'center' }}>
            <Box
              sx={{
                bgcolor: `${banner.accent}18`,
                color: banner.accent,
                px: 2,
                py: 0.5,
                borderRadius: 2,
                fontWeight: 900,
                fontSize: '0.85rem',
                letterSpacing: 1,
              }}
            >
              {banner.badge}
            </Box>

            <Typography
              variant="h3"
              component="h1"
              sx={{
                fontWeight: 900,
                color: '#09131F',
                fontSize: { xs: '1.75rem', sm: '2.5rem' },
              }}
            >
              {banner.title}
            </Typography>

            <Typography variant="body1" sx={{ color: '#64748B', fontWeight: 600, maxWidth: 420 }}>
              {banner.subtitle}
            </Typography>

            <Stack
              direction="row"
              spacing={1}
              sx={{
                alignItems: 'center',
                pt: 0.5,
                color: '#00629B',
              }}
            >
              <EmojiEventsIcon sx={{ fontSize: 20 }} />
              <Typography sx={{ fontWeight: 800, fontSize: '1.1rem' }}>
                {nickname} · {totalScore.toLocaleString()} points
              </Typography>
            </Stack>
          </Stack>

          <Box sx={{ width: '100%' }}>
            <LeaderboardPodium
              entries={entries}
              highlightParticipantId={participantId}
              size="compact"
            />
          </Box>

          <Box sx={{ width: '100%' }}>
            <Typography
              variant="h6"
              sx={{ fontWeight: 800, color: '#09131F', mb: 1.5, textAlign: 'center' }}
            >
              Final Standings
            </Typography>
            <LeaderboardList
              entries={entries}
              highlightParticipantId={participantId}
              maxRows={6}
              size="compact"
            />
          </Box>

          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1.5}
            sx={{ width: '100%', justifyContent: 'center' }}
          >
            <Button
              onClick={onPlayAgain}
              variant="contained"
              color="primary"
              startIcon={<ReplayIcon />}
              sx={{
                minHeight: 48,
                px: 3.5,
                fontWeight: 800,
                fontSize: '1rem',
                bgcolor: '#00629B',
                width: { xs: '100%', sm: 'auto' },
              }}
            >
              Play Again
            </Button>
            <Button
              onClick={onLeave}
              variant="outlined"
              color="inherit"
              startIcon={<LogoutIcon />}
              sx={{
                minHeight: 48,
                px: 3,
                fontWeight: 700,
                color: '#486581',
                borderColor: '#CBD5E1',
                width: { xs: '100%', sm: 'auto' },
              }}
            >
              Leave
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </>
  );
}

export default GameFinishedScreen;
