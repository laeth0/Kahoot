import { Stack } from '@mui/material';
import React from 'react';

import type { LeaderboardEntryResponse } from '../../realtime/events.ts';
import { PodiumSlot } from './PodiumSlot.tsx';

export interface LeaderboardPodiumProps {
  entries: LeaderboardEntryResponse[];
  highlightParticipantId?: string | null;
  size?: 'compact' | 'projector';
}

export const LeaderboardPodium: React.FC<LeaderboardPodiumProps> = ({
  entries,
  highlightParticipantId = null,
  size = 'compact',
}) => {
  const sorted = [...entries].sort((a, b) => a.rank - b.rank);
  const first = sorted.find((e) => e.rank === 1);
  const second = sorted.find((e) => e.rank === 2);
  const third = sorted.find((e) => e.rank === 3);

  if (!first) {
    return null;
  }

  const isProjector = size === 'projector';

  const firstSlot = (
    <PodiumSlot
      key={first.participantId}
      entry={first}
      rank={1}
      isFirstPlace
      height={isProjector ? { xs: 165, sm: 220 } : { xs: 135, sm: 175 }}
      avatarSize={isProjector ? { xs: 68, sm: 84 } : { xs: 58, sm: 70 }}
      pedestalGradient="linear-gradient(180deg, #FDE047 0%, #F59E0B 100%)"
      accentColor="#78350F"
      delaySec={0.45}
      isYou={first.participantId === highlightParticipantId}
      isProjector={isProjector}
    />
  );

  const secondSlot = second ? (
    <PodiumSlot
      key={second.participantId}
      entry={second}
      rank={2}
      isFirstPlace={false}
      height={isProjector ? { xs: 115, sm: 155 } : { xs: 95, sm: 125 }}
      avatarSize={isProjector ? { xs: 56, sm: 68 } : { xs: 48, sm: 58 }}
      pedestalGradient="linear-gradient(180deg, #F1F5F9 0%, #CBD5E1 100%)"
      accentColor="#475569"
      delaySec={0.25}
      isYou={second.participantId === highlightParticipantId}
      isProjector={isProjector}
    />
  ) : null;

  const thirdSlot = third ? (
    <PodiumSlot
      key={third.participantId}
      entry={third}
      rank={3}
      isFirstPlace={false}
      height={isProjector ? { xs: 90, sm: 120 } : { xs: 75, sm: 100 }}
      avatarSize={isProjector ? { xs: 50, sm: 60 } : { xs: 44, sm: 52 }}
      pedestalGradient="linear-gradient(180deg, #FED7AA 0%, #FB923C 100%)"
      accentColor="#9A3412"
      delaySec={0.1}
      isYou={third.participantId === highlightParticipantId}
      isProjector={isProjector}
    />
  ) : null;

  return (
    <Stack
      direction="row"
      spacing={{ xs: 1.5, sm: 3 }}
      sx={{
        alignItems: 'flex-end',
        justifyContent: 'center',
        width: '100%',
        pt: { xs: 2, sm: 3 },
        pb: 0,
        px: { xs: 1, sm: 2 },
        overflow: 'visible',
      }}
    >
      {secondSlot}
      {firstSlot}
      {thirdSlot}
    </Stack>
  );
};

export default LeaderboardPodium;
