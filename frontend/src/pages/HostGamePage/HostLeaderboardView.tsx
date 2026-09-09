import { Box, Card, Stack, Typography } from '@mui/material';

import type { LeaderboardResponse } from '../../realtime/events.ts';

export interface HostLeaderboardViewProps {
  leaderboard: LeaderboardResponse | null;
  heading?: string;
}

export function HostLeaderboardView({
  leaderboard,
  heading = 'Leaderboard',
}: HostLeaderboardViewProps) {
  const entries = leaderboard?.entries ?? [];

  return (
    <Card
      sx={{
        borderRadius: 4,
        border: '2px solid #E2E8F0',
        bgcolor: '#FFFFFF',
        p: { xs: 3, sm: 4 },
      }}
    >
      <Typography variant="h4" component="h1" sx={{ fontWeight: 900, color: '#09131F', mb: 3 }}>
        {heading}
      </Typography>

      {entries.length === 0 ? (
        <Typography sx={{ color: '#64748B', fontWeight: 600 }}>No scores yet.</Typography>
      ) : (
        <Stack spacing={1}>
          {entries.map((entry) => (
            <Stack
              key={entry.participantId}
              direction="row"
              spacing={2}
              sx={{
                alignItems: 'center',
                p: 1.5,
                borderRadius: 2,
                bgcolor: entry.rank <= 3 ? '#EEF7FC' : '#F8FAFC',
                border: '1px solid #E2E8F0',
              }}
            >
              <Box
                sx={{
                  width: 40,
                  height: 40,
                  borderRadius: '50%',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontWeight: 900,
                  color: '#FFFFFF',
                  bgcolor: entry.rank <= 3 ? '#00629B' : '#94A3B8',
                  flexShrink: 0,
                }}
              >
                {entry.rank}
              </Box>
              <Typography
                sx={{ fontWeight: 800, color: '#09131F', flexGrow: 1, minWidth: 0 }}
                noWrap
              >
                {entry.nickname}
              </Typography>
              <Typography sx={{ fontWeight: 900, color: '#00629B' }}>{entry.totalScore}</Typography>
            </Stack>
          ))}
        </Stack>
      )}
    </Card>
  );
}

export default HostLeaderboardView;
