import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import { Box, Paper, Stack, Typography } from '@mui/material';

import { LeaderboardList } from '../../components/LeaderboardList/index.ts';
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

  return (
    <Stack spacing={2.5}>
      <Paper
        elevation={0}
        sx={{
          borderRadius: 4,
          border: '1px solid #E2E8F0',
          bgcolor: '#FFFFFF',
          p: { xs: 3, sm: 4 },
          textAlign: 'center',
        }}
      >
        <Stack spacing={1} sx={{ alignItems: 'center' }}>
          <Typography
            variant="caption"
            sx={{ fontWeight: 800, letterSpacing: 1, color: '#64748B', textTransform: 'uppercase' }}
          >
            Your position
          </Typography>
          <Typography
            variant="h3"
            component="h1"
            sx={{ fontWeight: 900, color: '#00629B', lineHeight: 1 }}
          >
            {rank ? ordinal(rank) : '—'}
          </Typography>
          {rankDelta !== null && rankDelta !== 0 && (
            <Stack
              direction="row"
              spacing={0.5}
              sx={{
                alignItems: 'center',
                color: rankDelta > 0 ? '#059669' : '#DC2626',
                fontWeight: 800,
              }}
            >
              {rankDelta > 0 ? (
                <ArrowUpwardIcon sx={{ fontSize: 18 }} />
              ) : (
                <ArrowDownwardIcon sx={{ fontSize: 18 }} />
              )}
              <Typography sx={{ fontWeight: 800 }}>
                {Math.abs(rankDelta)} {rankDelta > 0 ? 'up' : 'down'} since last question
              </Typography>
            </Stack>
          )}
          <Typography sx={{ fontWeight: 700, color: '#486581' }}>{totalScore} points</Typography>
        </Stack>
      </Paper>

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

      <Stack direction="row" spacing={0.75} sx={{ justifyContent: 'center', alignItems: 'center' }}>
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
  );
}

export default PlayerLeaderboardView;
