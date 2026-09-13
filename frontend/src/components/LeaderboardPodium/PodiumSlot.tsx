import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import { Box, Chip, Stack, Typography } from '@mui/material';
import { motion } from 'motion/react';
import React from 'react';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';
import { RankBadge } from '../RankBadge/index.ts';

export interface PodiumSlotProps {
  entry: LeaderboardEntryResponse;
  rank: 1 | 2 | 3;
  isFirstPlace: boolean;
  height: { xs: number; sm: number };
  avatarSize: { xs: number; sm: number };
  pedestalGradient: string;
  accentColor: string;
  delaySec: number;
  isYou: boolean;
  isProjector: boolean;
}

const AVATAR_COLORS = ['#7C3AED', '#4F46E5', '#0284C7', '#059669', '#D97706', '#DC2626', '#DB2777'];

function getAvatarColor(name: string, rank: number): string {
  if (rank === 1) return '#3B82F6';
  if (rank === 2) return '#4338CA';
  if (rank === 3) return '#7C3AED';
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

function getInitial(nickname: string): string {
  return nickname.trim().charAt(0).toUpperCase() || '?';
}

export const PodiumSlot: React.FC<PodiumSlotProps> = ({
  entry,
  rank,
  isFirstPlace,
  height,
  avatarSize,
  pedestalGradient,
  accentColor,
  delaySec,
  isYou,
  isProjector,
}) => {
  const avatarBg = getAvatarColor(entry.nickname, rank);

  return (
    <motion.div
      initial={{ opacity: 0, y: 50, scale: 0.88 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={{
        duration: 0.6,
        delay: delaySec,
        ease: [0.34, 1.56, 0.64, 1],
      }}
      style={{
        flex: 1,
        maxWidth: isProjector ? 230 : 160,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
      }}
    >
      <Stack spacing={1} sx={{ alignItems: 'center', width: '100%', overflow: 'visible' }}>
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            mb: -0.5,
          }}
        >
          {isFirstPlace ? (
            <Box
              sx={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                animation: 'winnerTrophyFloat 2.6s ease-in-out infinite',
                '@keyframes winnerTrophyFloat': {
                  '0%, 100%': { transform: 'translateY(0) scale(1)' },
                  '50%': { transform: 'translateY(-6px) scale(1.06)' },
                },
                '@media (prefers-reduced-motion: reduce)': {
                  animation: 'none',
                },
              }}
            >
              <EmojiEventsIcon
                sx={{
                  fontSize: isProjector ? { xs: 44, sm: 54 } : { xs: 34, sm: 42 },
                  color: '#F59E0B',
                  filter: 'drop-shadow(0 4px 12px rgba(245, 158, 11, 0.5))',
                }}
              />
              <Box sx={{ mt: -0.5 }}>
                <RankBadge rank={1} size={isProjector ? 'medium' : 'small'} />
              </Box>
            </Box>
          ) : (
            <RankBadge rank={rank} size={isProjector ? 'medium' : 'small'} />
          )}
        </Box>

        <Box
          sx={{
            position: 'relative',
            width: avatarSize,
            height: avatarSize,
            borderRadius: '50%',
            bgcolor: isYou ? '#00629B' : avatarBg,
            color: '#FFFFFF',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 900,
            fontSize: isFirstPlace
              ? { xs: '1.5rem', sm: '1.9rem' }
              : { xs: '1.25rem', sm: '1.5rem' },
            border: isYou
              ? '3px solid #00629B'
              : isFirstPlace
                ? '3.5px solid #FDE68A'
                : '3px solid #FFFFFF',
            boxShadow: isFirstPlace
              ? '0 8px 24px rgba(245, 158, 11, 0.4)'
              : '0 4px 14px rgba(0, 0, 0, 0.12)',
          }}
        >
          {getInitial(entry.nickname)}
        </Box>

        <Stack spacing={0.25} sx={{ alignItems: 'center', width: '100%', minWidth: 0, px: 0.5 }}>
          <Typography
            sx={{
              fontWeight: 800,
              color: '#09131F',
              textAlign: 'center',
              fontSize: isFirstPlace
                ? { xs: '0.95rem', sm: '1.15rem' }
                : { xs: '0.825rem', sm: '0.975rem' },
              width: '100%',
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
              }}
            />
          )}

          <Typography
            sx={{
              fontWeight: 900,
              color: '#09131F',
              fontSize: isFirstPlace
                ? { xs: '1.1rem', sm: '1.35rem' }
                : { xs: '0.95rem', sm: '1.1rem' },
              fontVariantNumeric: 'tabular-nums',
            }}
          >
            {entry.totalScore.toLocaleString()} pts
          </Typography>
        </Stack>

        <Box
          sx={{
            width: '100%',
            height,
            borderRadius: '16px 16px 0 0',
            background: pedestalGradient,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'flex-start',
            pt: { xs: 1.5, sm: 2 },
            boxShadow: isFirstPlace
              ? '0 12px 30px rgba(245, 158, 11, 0.3)'
              : '0 6px 18px rgba(0, 0, 0, 0.08)',
            border: isFirstPlace
              ? '2px solid rgba(254, 240, 138, 0.8)'
              : '1px solid rgba(255, 255, 255, 0.7)',
            borderBottom: 'none',
            position: 'relative',
            overflow: 'hidden',
          }}
        >
          {isFirstPlace && (
            <Box
              sx={{
                position: 'absolute',
                top: 0,
                left: '-100%',
                width: '60%',
                height: '100%',
                background:
                  'linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.35), transparent)',
                transform: 'skewX(-25deg)',
                animation: 'shimmerSweep 3s infinite',
                '@keyframes shimmerSweep': {
                  '0%': { left: '-100%' },
                  '40%, 100%': { left: '200%' },
                },
                '@media (prefers-reduced-motion: reduce)': {
                  animation: 'none',
                },
              }}
            />
          )}

          <Typography
            sx={{
              fontWeight: 900,
              color: isFirstPlace ? '#92400E' : accentColor,
              fontSize: isFirstPlace
                ? { xs: '2.25rem', sm: '3rem' }
                : { xs: '1.8rem', sm: '2.4rem' },
              lineHeight: 1,
              opacity: 0.9,
            }}
          >
            {rank}
          </Typography>
        </Box>
      </Stack>
    </motion.div>
  );
};

export default PodiumSlot;
