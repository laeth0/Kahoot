import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import { Box, Stack, Typography } from '@mui/material';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';

export interface PodiumViewProps {
  entries: LeaderboardEntryResponse[];
  highlightParticipantId?: string | null;
}

interface Slot {
  entry: LeaderboardEntryResponse;
  height: number;
  medal: string;
  order: number;
}

const MEDALS = ['#B8860B', '#64748B', '#9A6A3A'];
const HEIGHTS = [128, 96, 76];
const ORDER = [2, 1, 3];

function initial(nickname: string): string {
  return nickname.trim().charAt(0).toUpperCase() || '?';
}

export function PodiumView({ entries, highlightParticipantId = null }: PodiumViewProps) {
  const top = [...entries].sort((a, b) => a.rank - b.rank).slice(0, 3);
  if (top.length === 0) {
    return null;
  }

  const slots: Slot[] = top.map((entry, index) => ({
    entry,
    height: HEIGHTS[index],
    medal: MEDALS[index],
    order: ORDER[index],
  }));
  slots.sort((a, b) => a.order - b.order);

  return (
    <Stack
      direction="row"
      spacing={{ xs: 1.5, sm: 3 }}
      sx={{ alignItems: 'flex-end', justifyContent: 'center', width: '100%', py: 2 }}
    >
      {slots.map(({ entry, height, medal }) => {
        const isYou = entry.participantId === highlightParticipantId;
        return (
          <Stack key={entry.participantId} spacing={1} sx={{ alignItems: 'center', maxWidth: 140 }}>
            <Box sx={{ color: medal }}>
              <EmojiEventsIcon sx={{ fontSize: entry.rank === 1 ? 40 : 30 }} />
            </Box>
            <Box
              sx={{
                width: 56,
                height: 56,
                borderRadius: '50%',
                bgcolor: isYou ? '#00629B' : '#EEF7FC',
                color: isYou ? '#FFFFFF' : '#00629B',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontWeight: 900,
                fontSize: '1.4rem',
                border: '2px solid #00629B',
              }}
            >
              {initial(entry.nickname)}
            </Box>
            <Typography sx={{ fontWeight: 800, color: '#09131F', textAlign: 'center' }} noWrap>
              {entry.nickname}
            </Typography>
            <Typography sx={{ fontWeight: 900, color: '#00629B' }}>{entry.totalScore}</Typography>
            <Box
              sx={{
                width: { xs: 72, sm: 96 },
                height,
                borderRadius: '8px 8px 0 0',
                bgcolor: isYou ? '#00629B' : '#CBD8E6',
                display: 'flex',
                alignItems: 'flex-start',
                justifyContent: 'center',
                pt: 1,
              }}
            >
              <Typography sx={{ fontWeight: 900, color: '#FFFFFF', fontSize: '1.5rem' }}>
                {entry.rank}
              </Typography>
            </Box>
          </Stack>
        );
      })}
    </Stack>
  );
}

export default PodiumView;
