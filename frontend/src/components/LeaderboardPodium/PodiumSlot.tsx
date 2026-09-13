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
  height?: { xs: number | string; sm: number | string } | string | number;
  avatarSize?: { xs: number | string; sm: number | string } | string | number;
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

  const resolvedHeight =
    height ??
    (isFirstPlace
      ? { xs: 'clamp(65px, 14vh, 180px)', sm: 'clamp(80px, 17vh, 210px)' }
      : rank === 2
        ? { xs: 'clamp(48px, 10vh, 130px)', sm: 'clamp(60px, 13vh, 155px)' }
        : { xs: 'clamp(36px, 7.5vh, 95px)', sm: 'clamp(46px, 9.5vh, 120px)' });

  const resolvedAvatarSize =
    avatarSize ??
    (isFirstPlace
      ? { xs: 'clamp(40px, 6.5vh, 64px)', sm: 'clamp(50px, 8vh, 78px)' }
      : rank === 2
        ? { xs: 'clamp(36px, 5.5vh, 56px)', sm: 'clamp(42px, 6.8vh, 66px)' }
        : { xs: 'clamp(32px, 5vh, 50px)', sm: 'clamp(38px, 6vh, 58px)' });

  return (
    <motion.div
      initial={{ opacity: 0, y: 40, scale: 0.9 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={{
        duration: 0.55,
        delay: delaySec,
        ease: [0.34, 1.56, 0.64, 1],
      }}
      style={{
        flex: 1,
        maxWidth: isProjector ? 240 : 180,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
      }}
    >
      <Stack spacing={0.75} sx={{ alignItems: 'center', width: '100%', overflow: 'visible' }}>
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            minHeight: isFirstPlace
              ? { xs: 'clamp(28px, 4vh, 48px)', sm: 'clamp(36px, 5vh, 56px)' }
              : { xs: 24, sm: 28 },
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
                  '50%': { transform: 'translateY(-5px) scale(1.05)' },
                },
                '@media (prefers-reduced-motion: reduce)': {
                  animation: 'none',
                },
              }}
            >
              <EmojiEventsIcon
                sx={{
                  fontSize: { xs: 'clamp(26px, 4vh, 42px)', sm: 'clamp(32px, 5vh, 50px)' },
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
            width: resolvedAvatarSize,
            height: resolvedAvatarSize,
            borderRadius: '50%',
            bgcolor: isYou ? '#00629B' : avatarBg,
            color: '#FFFFFF',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 900,
            fontSize: isFirstPlace
              ? { xs: 'clamp(1.1rem, 2.2vh, 1.6rem)', sm: 'clamp(1.3rem, 2.6vh, 1.9rem)' }
              : { xs: 'clamp(0.95rem, 1.8vh, 1.3rem)', sm: 'clamp(1.1rem, 2.1vh, 1.5rem)' },
            border: isYou
              ? '3px solid #00629B'
              : isFirstPlace
                ? '3px solid #FDE68A'
                : '2.5px solid #FFFFFF',
            boxShadow: isFirstPlace
              ? '0 8px 22px rgba(245, 158, 11, 0.35)'
              : '0 4px 12px rgba(0, 0, 0, 0.1)',
            flexShrink: 0,
          }}
        >
          {getInitial(entry.nickname)}
        </Box>

        <Stack spacing={0.2} sx={{ alignItems: 'center', width: '100%', minWidth: 0, px: 0.25 }}>
          <Typography
            sx={{
              fontWeight: 800,
              color: '#09131F',
              textAlign: 'center',
              fontSize: isFirstPlace
                ? { xs: 'clamp(0.8rem, 1.5vh, 1.05rem)', sm: 'clamp(0.9rem, 1.7vh, 1.15rem)' }
                : { xs: 'clamp(0.72rem, 1.3vh, 0.9rem)', sm: 'clamp(0.8rem, 1.4vh, 0.98rem)' },
              width: '100%',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
            title={entry.nickname}
          >
            {entry.nickname}
          </Typography>

          {isYou && (
            <Chip
              label="YOU"
              size="small"
              sx={{
                height: 16,
                fontWeight: 900,
                fontSize: '0.6rem',
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
                ? { xs: 'clamp(0.85rem, 1.6vh, 1.15rem)', sm: 'clamp(0.95rem, 1.8vh, 1.3rem)' }
                : { xs: 'clamp(0.75rem, 1.4vh, 0.95rem)', sm: 'clamp(0.825rem, 1.5vh, 1.05rem)' },
              fontVariantNumeric: 'tabular-nums',
            }}
          >
            {entry.totalScore.toLocaleString()} pts
          </Typography>
        </Stack>

        <Box
          sx={{
            width: '100%',
            height: resolvedHeight,
            minHeight: isFirstPlace ? 'clamp(45px, 9vh, 80px)' : 'clamp(30px, 6vh, 60px)',
            borderRadius: '16px 16px 0 0',
            background: pedestalGradient,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'flex-start',
            pt: { xs: 0.75, sm: 1.25 },
            boxShadow: isFirstPlace
              ? '0 10px 24px rgba(245, 158, 11, 0.28)'
              : '0 5px 14px rgba(0, 0, 0, 0.07)',
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
                ? { xs: 'clamp(1.6rem, 3.2vh, 2.5rem)', sm: 'clamp(1.9rem, 3.8vh, 3rem)' }
                : { xs: 'clamp(1.3rem, 2.6vh, 2rem)', sm: 'clamp(1.5rem, 3vh, 2.4rem)' },
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
