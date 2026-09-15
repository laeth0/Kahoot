import EmojiEventsOutlinedIcon from '@mui/icons-material/EmojiEventsOutlined';
import { Box, Card, Chip, Stack, Typography } from '@mui/material';
import { m, useReducedMotion } from 'motion/react';
import React from 'react';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';
import { LeaderboardPodium } from '../LeaderboardPodium/index.ts';
import { LeaderboardStandingsRow } from './LeaderboardStandingsRow.tsx';

export interface LeaderboardCardProps {
  entries: LeaderboardEntryResponse[];
  highlightParticipantId?: string | null;
  size?: 'compact' | 'projector';
  title?: string;
  subtitle?: string;
  maxStandingsRows?: number;
}

function CrownIcon({ size = 42 }: { size?: number }) {
  return (
    <svg
      width={size}
      height={size * 0.75}
      viewBox="0 0 48 36"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      style={{ filter: 'drop-shadow(0 4px 10px rgba(245, 158, 11, 0.45))' }}
    >
      <path d="M6 31C6 32.1 6.9 33 8 33H40C41.1 33 42 32.1 42 31V28H6V31Z" fill="#D97706" />
      <path d="M4 11L12.5 25H35.5L44 11L33 17L24 5L15 17L4 11Z" fill="url(#crownGradient)" />
      <circle cx="4" cy="10" r="3.5" fill="#FBBF24" stroke="#D97706" strokeWidth="1.5" />
      <circle cx="24" cy="4" r="4.5" fill="#FDE68A" stroke="#D97706" strokeWidth="1.5" />
      <circle cx="44" cy="10" r="3.5" fill="#FBBF24" stroke="#D97706" strokeWidth="1.5" />
      <circle cx="15" cy="17" r="2.5" fill="#F59E0B" />
      <circle cx="33" cy="17" r="2.5" fill="#F59E0B" />
      <defs>
        <linearGradient
          id="crownGradient"
          x1="24"
          y1="5"
          x2="24"
          y2="28"
          gradientUnits="userSpaceOnUse"
        >
          <stop stopColor="#FDE68A" />
          <stop offset="0.4" stopColor="#F59E0B" />
          <stop offset="1" stopColor="#D97706" />
        </linearGradient>
      </defs>
    </svg>
  );
}

export const LeaderboardCard: React.FC<LeaderboardCardProps> = ({
  entries,
  highlightParticipantId = null,
  size = 'compact',
  title = 'Leaderboard',
  subtitle = 'Great job everyone! Here are the current standings',
  maxStandingsRows = 10,
}) => {
  const shouldReduceMotion = useReducedMotion();
  const sorted = [...entries].sort((a, b) => a.rank - b.rank);
  const topThree = sorted.filter((e) => e.rank <= 3);
  const remaining = sorted.filter((e) => e.rank > 3);

  const visibleRemaining = remaining.slice(0, maxStandingsRows);
  const hiddenCount = remaining.length - visibleRemaining.length;

  const isProjector = size === 'projector';
  const hasRemaining = remaining.length > 0;

  const highlightedHidden =
    highlightParticipantId &&
    !topThree.some((e) => e.participantId === highlightParticipantId) &&
    !visibleRemaining.some((e) => e.participantId === highlightParticipantId)
      ? (sorted.find((e) => e.participantId === highlightParticipantId) ?? null)
      : null;

  const headerContent = (
    <Stack spacing={1} sx={{ alignItems: 'center', textAlign: 'center', mb: { xs: 1.5, sm: 2 } }}>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          animation: 'crownBob 3s ease-in-out infinite',
          '@keyframes crownBob': {
            '0%, 100%': { transform: 'translateY(0) rotate(0deg)' },
            '50%': { transform: 'translateY(-4px) rotate(1deg)' },
          },
          '@media (prefers-reduced-motion: reduce)': {
            animation: 'none',
          },
        }}
      >
        <CrownIcon size={isProjector ? 46 : 38} />
      </Box>

      <Box>
        <Typography
          variant="h3"
          component="h1"
          sx={{
            fontWeight: 900,
            color: '#09131F',
            fontSize: isProjector
              ? { xs: 'clamp(1.5rem, 3.2vh, 2.3rem)', sm: 'clamp(1.8rem, 3.8vh, 2.75rem)' }
              : { xs: 'clamp(1.35rem, 2.8vh, 1.9rem)', sm: 'clamp(1.5rem, 3.2vh, 2.25rem)' },
            lineHeight: 1.15,
            letterSpacing: '-0.02em',
          }}
        >
          {title}
        </Typography>

        <Typography
          variant="body1"
          sx={{
            color: '#64748B',
            fontWeight: 600,
            mt: 0.5,
            fontSize: isProjector
              ? { xs: '0.85rem', sm: 'clamp(0.9rem, 1.6vh, 1.1rem)' }
              : { xs: '0.8rem', sm: 'clamp(0.85rem, 1.5vh, 0.95rem)' },
          }}
        >
          {subtitle}
        </Typography>
      </Box>
    </Stack>
  );

  const standingsContent = hasRemaining ? (
    <Box
      sx={{
        width: '100%',
        bgcolor: '#F8FAFC',
        borderRadius: 4,
        p: { xs: 1.5, sm: 2 },
        border: '1px solid #EDF2F7',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <Stack
        direction="row"
        spacing={1}
        sx={{
          alignItems: 'center',
          justifyContent: 'space-between',
          mb: 1.25,
          px: 1,
        }}
      >
        <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
          <EmojiEventsOutlinedIcon sx={{ fontSize: 18, color: '#00629B' }} />
          <Typography
            sx={{
              fontWeight: 800,
              fontSize: '0.9rem',
              color: '#0F172A',
              letterSpacing: 0.2,
            }}
          >
            Standings
          </Typography>
        </Stack>

        <Chip
          label={`${sorted.length} players`}
          size="small"
          sx={{
            height: 20,
            fontWeight: 800,
            fontSize: '0.7rem',
            bgcolor: 'rgba(0, 98, 155, 0.08)',
            color: '#00629B',
          }}
        />
      </Stack>

      <Box
        sx={{
          overflowY: 'auto',
          maxHeight: { xs: 260, md: 'clamp(200px, 44vh, 380px)' },
          pr: 0.5,
          '&::-webkit-scrollbar': {
            width: 6,
          },
          '&::-webkit-scrollbar-thumb': {
            backgroundColor: '#CBD5E1',
            borderRadius: 3,
          },
        }}
      >
        <Stack spacing={0.75}>
          {visibleRemaining.map((entry, idx) => (
            <LeaderboardStandingsRow
              key={entry.participantId}
              entry={entry}
              index={idx}
              isYou={entry.participantId === highlightParticipantId}
              isProjector={isProjector}
            />
          ))}

          {highlightedHidden && (
            <>
              <Typography
                sx={{
                  textAlign: 'center',
                  color: '#94A3B8',
                  fontWeight: 700,
                  py: 0.25,
                }}
              >
                ⋯
              </Typography>
              <LeaderboardStandingsRow
                entry={highlightedHidden}
                index={visibleRemaining.length}
                isYou
                isProjector={isProjector}
              />
            </>
          )}

          {hiddenCount > 0 && !highlightedHidden && (
            <Typography
              sx={{
                textAlign: 'center',
                color: '#64748B',
                fontWeight: 600,
                pt: 0.5,
                fontSize: '0.85rem',
              }}
            >
              +{hiddenCount} more {hiddenCount === 1 ? 'player' : 'players'}
            </Typography>
          )}
        </Stack>
      </Box>
    </Box>
  ) : null;

  return (
    <m.div
      initial={shouldReduceMotion ? false : { opacity: 0, scale: 0.96 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: shouldReduceMotion ? 0 : 0.45, ease: [0.16, 1, 0.3, 1] }}
      style={{
        width: '100%',
        maxWidth: hasRemaining ? (isProjector ? 1160 : 1000) : isProjector ? 860 : 720,
        margin: '0 auto',
      }}
    >
      <Card
        elevation={0}
        sx={{
          borderRadius: { xs: 3, sm: 4 },
          border: '1.5px solid #E2E8F0',
          bgcolor: '#FFFFFF',
          px: { xs: 2, sm: 'clamp(16px, 2.5vw, 36px)' },
          pt: { xs: 2, sm: 'clamp(14px, 2vh, 28px)' },
          pb: { xs: 2, sm: 'clamp(14px, 2vh, 28px)' },
          boxShadow: '0 16px 40px rgba(0, 98, 155, 0.08)',
          position: 'relative',
          overflow: 'visible',
        }}
      >
        {hasRemaining ? (
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={{ xs: 2, md: 3 }}
            sx={{ alignItems: { xs: 'stretch', md: 'center' } }}
          >
            <Box sx={{ flex: { xs: '1 1 100%', md: '1 1 60%' }, minWidth: 0 }}>
              {headerContent}
              {topThree.length > 0 && (
                <Box sx={{ width: '100%' }}>
                  <LeaderboardPodium
                    entries={topThree}
                    highlightParticipantId={highlightParticipantId}
                    size={size}
                  />
                </Box>
              )}
            </Box>

            <Box sx={{ flex: { xs: '1 1 100%', md: '0 0 40%' }, minWidth: 0 }}>
              {standingsContent}
            </Box>
          </Stack>
        ) : (
          <Box sx={{ width: '100%' }}>
            {headerContent}
            {topThree.length > 0 && (
              <Box sx={{ width: '100%' }}>
                <LeaderboardPodium
                  entries={topThree}
                  highlightParticipantId={highlightParticipantId}
                  size={size}
                />
              </Box>
            )}
          </Box>
        )}
      </Card>
    </m.div>
  );
};

export default LeaderboardCard;
