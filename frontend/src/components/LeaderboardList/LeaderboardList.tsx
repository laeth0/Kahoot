import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import HorizontalRuleIcon from '@mui/icons-material/HorizontalRule';
import { Box, Stack, Typography } from '@mui/material';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';

export interface LeaderboardListProps {
  entries: LeaderboardEntryResponse[];
  highlightParticipantId?: string | null;
  rankDeltas?: Record<string, number> | null;
  maxRows?: number;
  size?: 'compact' | 'projector';
}

const RANK_COLORS: Record<number, string> = {
  1: '#B8860B',
  2: '#64748B',
  3: '#9A6A3A',
};

function RankDelta({ delta }: { delta: number }) {
  if (delta === 0) {
    return (
      <Stack direction="row" spacing={0.25} sx={{ alignItems: 'center', color: '#94A3B8' }}>
        <HorizontalRuleIcon sx={{ fontSize: 16 }} />
      </Stack>
    );
  }
  const improved = delta > 0;
  return (
    <Stack
      direction="row"
      spacing={0.25}
      sx={{ alignItems: 'center', color: improved ? '#059669' : '#DC2626', fontWeight: 800 }}
    >
      {improved ? (
        <ArrowUpwardIcon sx={{ fontSize: 16 }} />
      ) : (
        <ArrowDownwardIcon sx={{ fontSize: 16 }} />
      )}
      <Typography sx={{ fontWeight: 800, fontSize: '0.8rem' }}>{Math.abs(delta)}</Typography>
    </Stack>
  );
}

export function LeaderboardList({
  entries,
  highlightParticipantId = null,
  rankDeltas = null,
  maxRows = 10,
  size = 'compact',
}: LeaderboardListProps) {
  const sorted = [...entries].sort((a, b) => a.rank - b.rank);
  const visible = sorted.slice(0, maxRows);
  const hiddenCount = sorted.length - visible.length;

  const highlightedHidden =
    highlightParticipantId &&
    !visible.some((entry) => entry.participantId === highlightParticipantId)
      ? (sorted.find((entry) => entry.participantId === highlightParticipantId) ?? null)
      : null;

  const isProjector = size === 'projector';
  const rowPy = isProjector ? 1.5 : 1;
  const nameSize = isProjector ? '1.15rem' : '0.95rem';

  const renderRow = (entry: LeaderboardEntryResponse) => {
    const isYou = entry.participantId === highlightParticipantId;
    const delta = rankDeltas?.[entry.participantId];
    return (
      <Stack
        key={entry.participantId}
        direction="row"
        spacing={2}
        sx={{
          alignItems: 'center',
          px: 2,
          py: rowPy,
          borderRadius: 2,
          bgcolor: isYou ? 'rgba(0, 98, 155, 0.1)' : '#F8FAFC',
          border: isYou ? '2px solid #00629B' : '1px solid #E2E8F0',
        }}
      >
        <Box
          sx={{
            minWidth: isProjector ? 44 : 36,
            height: isProjector ? 44 : 36,
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 900,
            color: '#FFFFFF',
            bgcolor: RANK_COLORS[entry.rank] ?? '#94A3B8',
            flexShrink: 0,
          }}
        >
          {entry.rank}
        </Box>
        <Typography
          sx={{ fontWeight: 800, color: '#09131F', flexGrow: 1, minWidth: 0, fontSize: nameSize }}
          noWrap
        >
          {entry.nickname}
          {isYou && (
            <Box component="span" sx={{ ml: 1, color: '#00629B', fontWeight: 700 }}>
              (you)
            </Box>
          )}
        </Typography>
        {delta !== undefined && <RankDelta delta={delta} />}
        <Typography
          sx={{ fontWeight: 900, color: '#00629B', fontSize: isProjector ? '1.25rem' : '1rem' }}
        >
          {entry.totalScore}
        </Typography>
      </Stack>
    );
  };

  if (sorted.length === 0) {
    return <Typography sx={{ color: '#64748B', fontWeight: 600 }}>No scores yet.</Typography>;
  }

  return (
    <Stack spacing={1}>
      {visible.map(renderRow)}
      {highlightedHidden && (
        <>
          <Typography sx={{ textAlign: 'center', color: '#94A3B8', fontWeight: 700 }}>⋯</Typography>
          {renderRow(highlightedHidden)}
        </>
      )}
      {hiddenCount > 0 && (
        <Typography sx={{ textAlign: 'center', color: '#64748B', fontWeight: 600, pt: 0.5 }}>
          and {hiddenCount} more {hiddenCount === 1 ? 'player' : 'players'}
        </Typography>
      )}
    </Stack>
  );
}

export default LeaderboardList;
