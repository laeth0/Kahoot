import { Box, Chip, Stack, Typography } from '@mui/material';
import { motion } from 'motion/react';
import React from 'react';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';

export interface LeaderboardStandingsRowProps {
  entry: LeaderboardEntryResponse;
  index: number;
  isYou: boolean;
  isProjector?: boolean;
}

const ROW_AVATAR_PALETTE = [
  '#8B5CF6',
  '#EC4899',
  '#0EA5E9',
  '#10B981',
  '#F59E0B',
  '#6366F1',
  '#14B8A6',
];

function getRowAvatarColor(name: string, rank: number): string {
  let hash = rank;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return ROW_AVATAR_PALETTE[Math.abs(hash) % ROW_AVATAR_PALETTE.length];
}

function getInitial(nickname: string): string {
  return nickname.trim().charAt(0).toUpperCase() || '?';
}

export const LeaderboardStandingsRow: React.FC<LeaderboardStandingsRowProps> = ({
  entry,
  index,
  isYou,
  isProjector = false,
}) => {
  const avatarColor = getRowAvatarColor(entry.nickname, entry.rank);
  const avatarSize = isProjector ? 38 : 32;

  return (
    <motion.div
      initial={{ opacity: 0, x: -16 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{
        duration: 0.35,
        delay: 0.35 + index * 0.06,
        ease: [0.16, 1, 0.3, 1],
      }}
      style={{ width: '100%' }}
    >
      <Stack
        direction="row"
        spacing={2}
        sx={{
          alignItems: 'center',
          px: { xs: 2, sm: 3 },
          py: isProjector ? 1.5 : 1.1,
          borderRadius: 3,
          bgcolor: isYou ? 'rgba(0, 98, 155, 0.08)' : '#FFFFFF',
          border: isYou ? '2px solid #00629B' : '1px solid #F1F5F9',
          boxShadow: isYou ? '0 4px 14px rgba(0, 98, 155, 0.12)' : '0 1px 3px rgba(0, 0, 0, 0.02)',
          transition: 'transform 0.15s ease, box-shadow 0.15s ease',
          '&:hover': {
            bgcolor: isYou ? 'rgba(0, 98, 155, 0.1)' : '#F8FAFC',
          },
        }}
      >
        <Typography
          sx={{
            fontWeight: 800,
            fontSize: isProjector ? '1.15rem' : '0.95rem',
            color: '#475569',
            minWidth: 28,
            textAlign: 'center',
            fontVariantNumeric: 'tabular-nums',
          }}
        >
          {entry.rank}
        </Typography>

        <Box
          sx={{
            width: avatarSize,
            height: avatarSize,
            borderRadius: '50%',
            bgcolor: isYou ? '#00629B' : avatarColor,
            color: '#FFFFFF',
            fontWeight: 800,
            fontSize: isProjector ? '1rem' : '0.85rem',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
            boxShadow: '0 2px 6px rgba(0, 0, 0, 0.12)',
          }}
        >
          {getInitial(entry.nickname)}
        </Box>

        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexGrow: 1, minWidth: 0 }}>
          <Typography
            sx={{
              fontWeight: 700,
              fontSize: isProjector ? '1.15rem' : '0.95rem',
              color: '#09131F',
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
                height: 18,
                fontWeight: 900,
                fontSize: '0.65rem',
                bgcolor: '#00629B',
                color: '#FFFFFF',
                flexShrink: 0,
              }}
            />
          )}
        </Stack>

        <Typography
          sx={{
            fontWeight: 800,
            fontSize: isProjector ? '1.2rem' : '1rem',
            color: '#09131F',
            fontVariantNumeric: 'tabular-nums',
            flexShrink: 0,
          }}
        >
          {entry.totalScore.toLocaleString()} pts
        </Typography>
      </Stack>
    </motion.div>
  );
};

export default LeaderboardStandingsRow;
