import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import { Box, Paper, Stack, Typography } from '@mui/material';

import { CelebrationOverlay } from '../../components/CelebrationOverlay/index.ts';
import { LeaderboardList } from '../../components/LeaderboardList/index.ts';
import { PodiumView } from '../../components/PodiumView/index.ts';
import { SoundToggle } from '../../components/SoundToggle/index.ts';
import { useCelebration } from '../../hooks/useCelebration.ts';
import type { LeaderboardResponse } from '../../realtime/events.ts';
import { ordinal } from '../../utils/rank.ts';

export interface PlayerLeaderboardViewProps {
  participantId: string;
  rank: number | null;
  totalScore: number;
  rankDelta: number | null;
  leaderboard: LeaderboardResponse | null;
}

export function PlayerLeaderboardView({
  participantId,
  rank,
  totalScore,
  rankDelta,
  leaderboard,
}: PlayerLeaderboardViewProps) {
  const entries = leaderboard?.entries ?? [];
  const deltaMap = rankDelta !== null ? { [participantId]: rankDelta } : null;

  useCelebration({
    variant: 'leaderboard',
    triggerKey: `${rank}_${totalScore}_${entries.length}`,
    autoPlaySound: true,
  });

  const getRankHeadline = () => {
    if (rank === 1) {
      return {
        title: "You're in 1st Place!",
        subtitle: 'Incredible performance! Keep defending the top spot!',
      };
    }
    if (rank === 2 || rank === 3) {
      return {
        title: "You're on the Podium!",
        subtitle: 'You are right in the top 3 — keep pushing!',
      };
    }
    if (rankDelta && rankDelta > 0) {
      return {
        title: 'Moving Up the Board!',
        subtitle: `Climbed +${rankDelta} ranks on that question!`,
      };
    }
    return { title: 'Scores Updated!', subtitle: 'Get ready for the next round.' };
  };

  const headline = getRankHeadline();

  return (
    <>
      <CelebrationOverlay
        variant="leaderboard"
        triggerKey={`${rank}_${totalScore}_${entries.length}`}
      />

      <Stack spacing={2.5}>
        <Paper
          elevation={0}
          sx={{
            position: 'relative',
            borderRadius: 4,
            border: rank && rank <= 3 ? '2px solid #00629B' : '1px solid #E2E8F0',
            bgcolor: '#FFFFFF',
            p: { xs: 3, sm: 4 },
            textAlign: 'center',
            boxShadow: '0 4px 20px rgba(0, 98, 155, 0.08)',
          }}
        >
          <Box sx={{ position: 'absolute', top: 16, right: 16 }}>
            <SoundToggle size="small" />
          </Box>

          <Stack spacing={1.5} sx={{ alignItems: 'center' }}>
            {rank && rank <= 3 && (
              <Box
                sx={{
                  color: rank === 1 ? '#F59E0B' : rank === 2 ? '#64748B' : '#EA580C',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <EmojiEventsIcon sx={{ fontSize: 36 }} />
              </Box>
            )}

            <Typography
              variant="caption"
              sx={{
                fontWeight: 800,
                letterSpacing: 1.5,
                color: '#00629B',
                textTransform: 'uppercase',
              }}
            >
              {headline.title}
            </Typography>

            <Typography
              variant="h3"
              component="h1"
              sx={{
                fontWeight: 900,
                color: '#09131F',
                lineHeight: 1,
              }}
            >
              {rank ? ordinal(rank) : '—'}
            </Typography>

            <Typography variant="body2" sx={{ color: '#64748B', fontWeight: 600, maxWidth: 360 }}>
              {headline.subtitle}
            </Typography>

            {rankDelta !== null && rankDelta !== 0 && (
              <Stack
                direction="row"
                spacing={0.5}
                sx={{
                  alignItems: 'center',
                  color: rankDelta > 0 ? '#059669' : '#DC2626',
                  fontWeight: 800,
                  bgcolor: rankDelta > 0 ? 'rgba(5, 150, 105, 0.08)' : 'rgba(220, 38, 38, 0.08)',
                  px: 1.5,
                  py: 0.5,
                  borderRadius: 2,
                }}
              >
                {rankDelta > 0 ? (
                  <ArrowUpwardIcon sx={{ fontSize: 18 }} />
                ) : (
                  <ArrowDownwardIcon sx={{ fontSize: 18 }} />
                )}
                <Typography sx={{ fontWeight: 800, fontSize: '0.9rem' }}>
                  {Math.abs(rankDelta)} {rankDelta > 0 ? 'up' : 'down'} since last question
                </Typography>
              </Stack>
            )}

            <Typography sx={{ fontWeight: 800, color: '#00629B', fontSize: '1.25rem' }}>
              {totalScore.toLocaleString()} points
            </Typography>
          </Stack>
        </Paper>

        {entries.length >= 3 && (
          <Paper
            elevation={0}
            sx={{
              borderRadius: 4,
              border: '1px solid #E2E8F0',
              bgcolor: '#FFFFFF',
              p: { xs: 2, sm: 3 },
            }}
          >
            <Typography
              variant="subtitle1"
              sx={{ fontWeight: 900, color: '#09131F', textAlign: 'center', mb: 1 }}
            >
              Top Performers
            </Typography>
            <PodiumView entries={entries} highlightParticipantId={participantId} size="compact" />
          </Paper>
        )}

        {entries.length > 0 && (
          <Box>
            <LeaderboardList
              entries={entries}
              highlightParticipantId={participantId}
              rankDeltas={deltaMap}
              maxRows={5}
              size="compact"
            />
          </Box>
        )}

        <Stack
          direction="row"
          spacing={0.75}
          sx={{ justifyContent: 'center', alignItems: 'center' }}
        >
          {[0, 1, 2].map((dot) => (
            <Box
              key={dot}
              aria-hidden
              sx={{
                width: 8,
                height: 8,
                borderRadius: '50%',
                bgcolor: '#00629B',
                animation: `lbBlink 1.2s ease-in-out ${dot * 0.2}s infinite`,
                '@keyframes lbBlink': {
                  '0%, 100%': { opacity: 0.25 },
                  '50%': { opacity: 1 },
                },
                '@media (prefers-reduced-motion: reduce)': { animation: 'none', opacity: 0.5 },
              }}
            />
          ))}
          <Typography sx={{ ml: 1, color: '#64748B', fontWeight: 600 }}>
            Next question soon…
          </Typography>
        </Stack>
      </Stack>
    </>
  );
}

export default PlayerLeaderboardView;
