import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import HorizontalRuleIcon from '@mui/icons-material/HorizontalRule';
import { Box, Chip, Stack, Typography } from '@mui/material';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';

export interface LeaderboardListProps {
  entries: LeaderboardEntryResponse[];
  highlightParticipantId?: string | null;
  rankDeltas?: Record<string, number> | null;
  maxRows?: number;
  size?: 'compact' | 'projector';
}

const RANK_BADGE_COLORS: Record<number, { bg: string; text: string }> = {
  1: { bg: '#F59E0B', text: '#FFFFFF' },
  2: { bg: '#64748B', text: '#FFFFFF' },
  3: { bg: '#EA580C', text: '#FFFFFF' },
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
  const rowPy = isProjector ? 1.75 : 1.1;
  const nameSize = isProjector ? '1.25rem' : '0.975rem';

  const renderRow = (entry: LeaderboardEntryResponse, index: number) => {
    const isYou = entry.participantId === highlightParticipantId;
    const delta = rankDeltas?.[entry.participantId];
    const badgeStyle = RANK_BADGE_COLORS[entry.rank] ?? { bg: '#CBD5E1', text: '#09131F' };

    return (
      <Stack
        key={entry.participantId}
        direction="row"
        spacing={2}
        sx={{
          alignItems: 'center',
          px: { xs: 2, sm: 2.5 },
          py: rowPy,
          borderRadius: 3,
          bgcolor: isYou ? 'rgba(0, 98, 155, 0.08)' : '#FFFFFF',
          border: isYou
            ? '2px solid #00629B'
            : entry.rank === 1
              ? '2px solid #FDE68A'
              : '1px solid #E2E8F0',
          boxShadow: isYou ? '0 4px 14px rgba(0, 98, 155, 0.15)' : '0 2px 6px rgba(0, 0, 0, 0.03)',
          animation: `leaderboardRowSlide 0.4s cubic-bezier(0.16, 1, 0.3, 1) ${index * 0.05}s both`,
          '@keyframes leaderboardRowSlide': {
            '0%': {
              opacity: 0,
              transform: 'translateX(-16px)',
            },
            '100%': {
              opacity: 1,
              transform: 'translateX(0)',
            },
          },
          '@media (prefers-reduced-motion: reduce)': {
            animation: 'none',
          },
        }}
      >
        <Box
          sx={{
            minWidth: isProjector ? 46 : 38,
            height: isProjector ? 46 : 38,
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 900,
            fontSize: isProjector ? '1.2rem' : '1rem',
            color: badgeStyle.text,
            bgcolor: badgeStyle.bg,
            boxShadow: entry.rank <= 3 ? '0 2px 8px rgba(0, 0, 0, 0.15)' : 'none',
            flexShrink: 0,
          }}
        >
          {entry.rank}
        </Box>

        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexGrow: 1, minWidth: 0 }}>
          <Typography
            sx={{
              fontWeight: 800,
              color: '#09131F',
              fontSize: nameSize,
            }}
            noWrap
          >
            {entry.nickname}
          </Typography>

          {isYou && (
            <Chip
              label="YOU"
              size="small"
              sx={{
                height: 20,
                fontWeight: 900,
                fontSize: '0.7rem',
                bgcolor: '#00629B',
                color: '#FFFFFF',
                flexShrink: 0,
              }}
            />
          )}
        </Stack>

        {delta !== undefined && <RankDelta delta={delta} />}

        <Typography
          sx={{
            fontWeight: 900,
            color: '#00629B',
            fontSize: isProjector ? '1.35rem' : '1.05rem',
            fontVariantNumeric: 'tabular-nums',
          }}
        >
          {entry.totalScore.toLocaleString()}
        </Typography>
      </Stack>
    );
  };

  if (sorted.length === 0) {
    return <Typography sx={{ color: '#64748B', fontWeight: 600 }}>No scores yet.</Typography>;
  }

  return (
    <Stack spacing={1.25}>
      {visible.map((entry, idx) => renderRow(entry, idx))}
      {highlightedHidden && (
        <>
          <Typography sx={{ textAlign: 'center', color: '#94A3B8', fontWeight: 700 }}>⋯</Typography>
          {renderRow(highlightedHidden, visible.length)}
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
