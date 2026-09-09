import LogoutIcon from '@mui/icons-material/Logout';
import ReplayIcon from '@mui/icons-material/Replay';
import { Box, Button, Paper, Stack, Typography } from '@mui/material';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';
import { ordinal } from '../../utils/rank.ts';
import { LeaderboardList } from '../LeaderboardList/index.ts';
import { PodiumView } from '../PodiumView/index.ts';

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
  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 4,
        border: '1px solid #E2E8F0',
        bgcolor: '#FFFFFF',
        p: { xs: 3, sm: 4 },
      }}
    >
      <Stack spacing={3} sx={{ alignItems: 'center' }}>
        <Stack spacing={0.5} sx={{ alignItems: 'center', textAlign: 'center' }}>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 900, color: '#09131F' }}>
            {rank ? `You finished ${ordinal(rank)}!` : 'That is a wrap!'}
          </Typography>
          <Typography sx={{ color: '#486581', fontWeight: 700 }}>
            {nickname} · {totalScore} points
          </Typography>
        </Stack>

        <PodiumView entries={entries} highlightParticipantId={participantId} />

        <Box sx={{ width: '100%' }}>
          <LeaderboardList
            entries={entries}
            highlightParticipantId={participantId}
            maxRows={5}
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
            sx={{ minHeight: 48, px: 3, fontWeight: 700, width: { xs: '100%', sm: 'auto' } }}
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
  );
}

export default GameFinishedScreen;
