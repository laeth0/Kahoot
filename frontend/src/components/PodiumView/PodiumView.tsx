import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import StarIcon from '@mui/icons-material/Star';
import { Box, Chip, Stack, Typography } from '@mui/material';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';

export interface PodiumViewProps {
  entries: LeaderboardEntryResponse[];
  highlightParticipantId?: string | null;
  size?: 'compact' | 'projector';
}

interface PodiumSlotConfig {
  entry: LeaderboardEntryResponse;
  height: { xs: number; sm: number };
  pedestalGradient: string;
  avatarSize: { xs: number; sm: number };
  accentColor: string;
  order: number;
  delaySec: number;
  isFirstPlace: boolean;
}

function initial(nickname: string): string {
  return nickname.trim().charAt(0).toUpperCase() || '?';
}

export function PodiumView({
  entries,
  highlightParticipantId = null,
  size = 'compact',
}: PodiumViewProps) {
  const top = [...entries].sort((a, b) => a.rank - b.rank).slice(0, 3);
  if (top.length === 0) {
    return null;
  }

  const isProjector = size === 'projector';

  const first = top.find((e) => e.rank === 1);
  const second = top.find((e) => e.rank === 2);
  const third = top.find((e) => e.rank === 3);

  const slots: PodiumSlotConfig[] = [];

  if (second) {
    slots.push({
      entry: second,
      height: isProjector ? { xs: 110, sm: 150 } : { xs: 90, sm: 120 },
      pedestalGradient: 'linear-gradient(180deg, #94A3B8 0%, #64748B 100%)',
      avatarSize: isProjector ? { xs: 58, sm: 70 } : { xs: 50, sm: 60 },
      accentColor: '#64748B',
      order: 1,
      delaySec: 0.25,
      isFirstPlace: false,
    });
  }

  if (first) {
    slots.push({
      entry: first,
      height: isProjector ? { xs: 155, sm: 210 } : { xs: 130, sm: 170 },
      pedestalGradient: 'linear-gradient(180deg, #FBBF24 0%, #D97706 100%)',
      avatarSize: isProjector ? { xs: 74, sm: 90 } : { xs: 64, sm: 76 },
      accentColor: '#F59E0B',
      order: 2,
      delaySec: 0.5,
      isFirstPlace: true,
    });
  }

  if (third) {
    slots.push({
      entry: third,
      height: isProjector ? { xs: 85, sm: 115 } : { xs: 70, sm: 95 },
      pedestalGradient: 'linear-gradient(180deg, #F97316 0%, #C2410C 100%)',
      avatarSize: isProjector ? { xs: 52, sm: 62 } : { xs: 46, sm: 54 },
      accentColor: '#EA580C',
      order: 3,
      delaySec: 0.05,
      isFirstPlace: false,
    });
  }

  slots.sort((a, b) => a.order - b.order);

  return (
    <Stack
      direction="row"
      spacing={{ xs: 1.5, sm: 3 }}
      sx={{
        alignItems: 'flex-end',
        justifyContent: 'center',
        width: '100%',
        pt: { xs: 3, sm: 4 },
        pb: { xs: 1, sm: 2 },
        px: 1,
        overflow: 'visible',
      }}
    >
      {slots.map((slot) => {
        const isYou = slot.entry.participantId === highlightParticipantId;
        return (
          <Stack
            key={slot.entry.participantId}
            spacing={1}
            sx={{
              alignItems: 'center',
              flex: 1,
              maxWidth: isProjector ? { xs: 160, sm: 220 } : { xs: 120, sm: 160 },
              minWidth: 0,
              animation: `podiumEntrance 0.7s cubic-bezier(0.34, 1.56, 0.64, 1) ${slot.delaySec}s both`,
              '@keyframes podiumEntrance': {
                '0%': {
                  opacity: 0,
                  transform: 'translateY(50px) scale(0.85)',
                },
                '100%': {
                  opacity: 1,
                  transform: 'translateY(0) scale(1)',
                },
              },
              '@media (prefers-reduced-motion: reduce)': {
                animation: 'none',
              },
            }}
          >
            <Box
              sx={{
                position: 'relative',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: slot.accentColor,
                mb: -0.5,
              }}
            >
              {slot.isFirstPlace ? (
                <Box
                  sx={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    animation: 'crownFloat 2.4s ease-in-out infinite',
                    '@keyframes crownFloat': {
                      '0%, 100%': { transform: 'translateY(0) scale(1)' },
                      '50%': { transform: 'translateY(-6px) scale(1.08)' },
                    },
                    '@media (prefers-reduced-motion: reduce)': {
                      animation: 'none',
                    },
                  }}
                >
                  <EmojiEventsIcon
                    sx={{
                      fontSize: isProjector ? { xs: 44, sm: 56 } : { xs: 38, sm: 46 },
                      filter: 'drop-shadow(0 4px 10px rgba(245, 158, 11, 0.45))',
                    }}
                  />
                </Box>
              ) : (
                <EmojiEventsIcon
                  sx={{
                    fontSize: isProjector ? { xs: 32, sm: 40 } : { xs: 26, sm: 32 },
                    opacity: 0.9,
                  }}
                />
              )}
            </Box>

            <Box
              sx={{
                position: 'relative',
                width: slot.avatarSize,
                height: slot.avatarSize,
                borderRadius: '50%',
                bgcolor: isYou ? '#00629B' : '#FFFFFF',
                color: isYou ? '#FFFFFF' : slot.accentColor,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontWeight: 900,
                fontSize: slot.isFirstPlace
                  ? { xs: '1.6rem', sm: '2rem' }
                  : { xs: '1.3rem', sm: '1.6rem' },
                border: isYou ? '3px solid #00629B' : `3px solid ${slot.accentColor}`,
                boxShadow: slot.isFirstPlace
                  ? '0 6px 20px rgba(245, 158, 11, 0.35)'
                  : '0 4px 12px rgba(0, 0, 0, 0.08)',
              }}
            >
              {initial(slot.entry.nickname)}
              {slot.isFirstPlace && (
                <Box
                  sx={{
                    position: 'absolute',
                    top: -6,
                    right: -6,
                    bgcolor: '#F59E0B',
                    color: '#FFFFFF',
                    borderRadius: '50%',
                    width: 22,
                    height: 22,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    boxShadow: '0 2px 6px rgba(0, 0, 0, 0.2)',
                  }}
                >
                  <StarIcon sx={{ fontSize: 14 }} />
                </Box>
              )}
            </Box>

            <Stack spacing={0.25} sx={{ alignItems: 'center', width: '100%', minWidth: 0 }}>
              <Typography
                sx={{
                  fontWeight: 900,
                  color: '#09131F',
                  textAlign: 'center',
                  fontSize: slot.isFirstPlace
                    ? { xs: '1rem', sm: '1.2rem' }
                    : { xs: '0.875rem', sm: '1.05rem' },
                  width: '100%',
                }}
                noWrap
              >
                {slot.entry.nickname}
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
                  }}
                />
              )}

              <Typography
                sx={{
                  fontWeight: 900,
                  color: '#00629B',
                  fontSize: slot.isFirstPlace
                    ? { xs: '1.1rem', sm: '1.35rem' }
                    : { xs: '0.95rem', sm: '1.15rem' },
                }}
              >
                {slot.entry.totalScore.toLocaleString()}
              </Typography>
            </Stack>

            <Box
              sx={{
                width: '100%',
                height: slot.height,
                borderRadius: '12px 12px 0 0',
                background: slot.pedestalGradient,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'flex-start',
                pt: { xs: 1, sm: 1.5 },
                boxShadow: slot.isFirstPlace
                  ? '0 10px 28px rgba(245, 158, 11, 0.35)'
                  : '0 6px 18px rgba(0, 0, 0, 0.1)',
                border: '1px solid rgba(255, 255, 255, 0.25)',
                borderBottom: 'none',
              }}
            >
              <Typography
                sx={{
                  fontWeight: 900,
                  color: '#FFFFFF',
                  fontSize: slot.isFirstPlace
                    ? { xs: '2rem', sm: '2.75rem' }
                    : { xs: '1.6rem', sm: '2.2rem' },
                  lineHeight: 1,
                  textShadow: '0 2px 6px rgba(0, 0, 0, 0.3)',
                }}
              >
                {slot.entry.rank}
              </Typography>
            </Box>
          </Stack>
        );
      })}
    </Stack>
  );
}

export default PodiumView;
