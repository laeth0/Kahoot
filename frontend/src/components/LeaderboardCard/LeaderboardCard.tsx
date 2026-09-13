import { Box, Card, Stack, Typography } from '@mui/material';
import { motion } from 'motion/react';
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
  const sorted = [...entries].sort((a, b) => a.rank - b.rank);
  const topThree = sorted.filter((e) => e.rank <= 3);
  const remaining = sorted.filter((e) => e.rank > 3);

  const visibleRemaining = remaining.slice(0, maxStandingsRows);
  const hiddenCount = remaining.length - visibleRemaining.length;

  const isProjector = size === 'projector';

  const highlightedHidden =
    highlightParticipantId &&
    !topThree.some((e) => e.participantId === highlightParticipantId) &&
    !visibleRemaining.some((e) => e.participantId === highlightParticipantId)
      ? (sorted.find((e) => e.participantId === highlightParticipantId) ?? null)
      : null;

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.96 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.45, ease: [0.16, 1, 0.3, 1] }}
      style={{ width: '100%', maxWidth: isProjector ? 960 : 760, margin: '0 auto' }}
    >
      <Card
        elevation={0}
        sx={{
          borderRadius: { xs: 4, sm: 5 },
          border: '1.5px solid #E2E8F0',
          bgcolor: '#FFFFFF',
          px: { xs: 2.5, sm: 5 },
          pt: { xs: 3.5, sm: 4.5 },
          pb: { xs: 3.5, sm: 4.5 },
          boxShadow: '0 16px 40px rgba(0, 98, 155, 0.08)',
          position: 'relative',
          overflow: 'visible',
        }}
      >
        <Stack spacing={2} sx={{ alignItems: 'center', textAlign: 'center', mb: 3 }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              animation: 'crownBob 3s ease-in-out infinite',
              '@keyframes crownBob': {
                '0%, 100%': { transform: 'translateY(0) rotate(0deg)' },
                '50%': { transform: 'translateY(-5px) rotate(1deg)' },
              },
              '@media (prefers-reduced-motion: reduce)': {
                animation: 'none',
              },
            }}
          >
            <CrownIcon size={isProjector ? 52 : 44} />
          </Box>

          <Box>
            <Typography
              variant="h3"
              component="h1"
              sx={{
                fontWeight: 900,
                color: '#09131F',
                fontSize: isProjector
                  ? { xs: '2rem', sm: '2.75rem' }
                  : { xs: '1.75rem', sm: '2.25rem' },
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
                mt: 0.75,
                fontSize: isProjector ? '1.1rem' : '0.95rem',
              }}
            >
              {subtitle}
            </Typography>
          </Box>
        </Stack>

        {topThree.length > 0 && (
          <Box sx={{ width: '100%', mb: remaining.length > 0 ? 3 : 1 }}>
            <LeaderboardPodium
              entries={topThree}
              highlightParticipantId={highlightParticipantId}
              size={size}
            />
          </Box>
        )}

        {remaining.length > 0 && (
          <Box
            sx={{
              width: '100%',
              bgcolor: '#F8FAFC',
              borderRadius: 4,
              p: { xs: 1.5, sm: 2.5 },
              border: '1px solid #EDF2F7',
            }}
          >
            <Stack spacing={1}>
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
                      py: 0.5,
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
                    pt: 1,
                    fontSize: '0.9rem',
                  }}
                >
                  +{hiddenCount} more {hiddenCount === 1 ? 'player' : 'players'}
                </Typography>
              )}
            </Stack>
          </Box>
        )}
      </Card>
    </motion.div>
  );
};

export default LeaderboardCard;
