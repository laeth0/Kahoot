import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import { Box, Typography } from '@mui/material';
import React from 'react';

export interface RankBadgeProps {
  rank: number;
  size?: 'small' | 'medium' | 'large';
}

const BADGE_CONFIG: Record<
  number,
  {
    bg: string;
    border: string;
    text: string;
    shadow: string;
    ribbon: string;
  }
> = {
  1: {
    bg: 'linear-gradient(135deg, #FBBF24 0%, #F59E0B 50%, #D97706 100%)',
    border: '#FDE68A',
    text: '#FFFFFF',
    shadow: '0 4px 12px rgba(245, 158, 11, 0.45)',
    ribbon: '#D97706',
  },
  2: {
    bg: 'linear-gradient(135deg, #CBD5E1 0%, #94A3B8 50%, #64748B 100%)',
    border: '#F1F5F9',
    text: '#FFFFFF',
    shadow: '0 4px 12px rgba(100, 116, 139, 0.35)',
    ribbon: '#475569',
  },
  3: {
    bg: 'linear-gradient(135deg, #FDBA74 0%, #EA580C 50%, #C2410C 100%)',
    border: '#FFEDD5',
    text: '#FFFFFF',
    shadow: '0 4px 12px rgba(234, 88, 12, 0.35)',
    ribbon: '#9A3412',
  },
};

export const RankBadge: React.FC<RankBadgeProps> = ({ rank, size = 'medium' }) => {
  const isTopThree = rank >= 1 && rank <= 3;
  const config = BADGE_CONFIG[rank] ?? {
    bg: '#E2E8F0',
    border: '#CBD5E1',
    text: '#09131F',
    shadow: 'none',
    ribbon: 'transparent',
  };

  const dimensions = {
    small: { badgeSize: 26, fontSize: '0.8rem', iconSize: 14 },
    medium: { badgeSize: 34, fontSize: '1rem', iconSize: 18 },
    large: { badgeSize: 44, fontSize: '1.25rem', iconSize: 24 },
  }[size];

  if (!isTopThree) {
    return (
      <Box
        sx={{
          minWidth: dimensions.badgeSize,
          height: dimensions.badgeSize,
          borderRadius: '50%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          bgcolor: '#F1F5F9',
          color: '#475569',
          fontWeight: 900,
          fontSize: dimensions.fontSize,
          border: '1px solid #CBD5E1',
          flexShrink: 0,
        }}
      >
        {rank}
      </Box>
    );
  }

  return (
    <Box
      sx={{
        position: 'relative',
        display: 'inline-flex',
        flexDirection: 'column',
        alignItems: 'center',
        flexShrink: 0,
      }}
    >
      <Box
        sx={{
          position: 'relative',
          width: dimensions.badgeSize,
          height: dimensions.badgeSize,
          borderRadius: '50%',
          background: config.bg,
          border: `2px solid ${config.border}`,
          boxShadow: config.shadow,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          color: config.text,
          fontWeight: 900,
          fontSize: dimensions.fontSize,
          zIndex: 2,
        }}
      >
        {rank === 1 ? (
          <EmojiEventsIcon sx={{ fontSize: dimensions.iconSize, color: '#FFFFFF' }} />
        ) : (
          <Typography
            component="span"
            sx={{
              fontWeight: 900,
              fontSize: dimensions.fontSize,
              lineHeight: 1,
              textShadow: '0 1px 2px rgba(0,0,0,0.3)',
            }}
          >
            {rank}
          </Typography>
        )}
      </Box>

      <Box
        sx={{
          position: 'relative',
          display: 'flex',
          justifyContent: 'center',
          mt: -0.6,
          zIndex: 1,
        }}
      >
        <Box
          sx={{
            width: 0,
            height: 0,
            borderLeft: `${dimensions.badgeSize * 0.22}px solid transparent`,
            borderRight: `${dimensions.badgeSize * 0.22}px solid transparent`,
            borderTop: `${dimensions.badgeSize * 0.35}px solid ${config.ribbon}`,
            filter: 'drop-shadow(0 2px 2px rgba(0,0,0,0.15))',
          }}
        />
      </Box>
    </Box>
  );
};

export default RankBadge;
