import CloseIcon from '@mui/icons-material/Close';
import PersonIcon from '@mui/icons-material/Person';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import { Avatar, Box, IconButton, Stack, Tooltip, Typography } from '@mui/material';
import { memo } from 'react';

import type { GameParticipantResponse } from '../../realtime/events.ts';

export interface ParticipantTileProps {
  participant: GameParticipantResponse;
  onRemove: (participantId: string, nickname: string) => void;
  disabled?: boolean;
}

const AVATAR_COLORS = [
  '#00629B',
  '#0284C7',
  '#028487',
  '#2563EB',
  '#4F46E5',
  '#7C3AED',
  '#059669',
  '#D97706',
  '#DC2626',
];

function getAvatarColor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  const index = Math.abs(hash) % AVATAR_COLORS.length;
  return AVATAR_COLORS[index];
}

export const ParticipantTile = memo(function ParticipantTile({
  participant,
  onRemove,
  disabled = false,
}: ParticipantTileProps) {
  const { id, nickname, isConnected } = participant;
  const avatarBg = getAvatarColor(nickname);
  const initial = nickname.charAt(0).toUpperCase();

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        p: 1.25,
        px: 2,
        bgcolor: '#ffffff',
        border: '1px solid',
        borderColor: isConnected ? '#E2E8F0' : '#FBD38D',
        borderRadius: 3,
        boxShadow: '0 2px 6px rgba(0, 0, 0, 0.04)',
        transition: 'all 0.15s ease',
        opacity: isConnected ? 1 : 0.65,
        '&:hover': {
          boxShadow: '0 4px 12px rgba(0, 98, 155, 0.12)',
          borderColor: '#00629B',
          '& .remove-btn': {
            opacity: 1,
          },
        },
      }}
    >
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', minWidth: 0, flex: 1 }}>
        <Box sx={{ position: 'relative' }}>
          <Avatar
            sx={{
              width: 38,
              height: 38,
              bgcolor: avatarBg,
              fontSize: '1rem',
              fontWeight: 700,
              color: '#ffffff',
            }}
          >
            {initial || <PersonIcon fontSize="small" />}
          </Avatar>
          <Box
            sx={{
              position: 'absolute',
              bottom: -1,
              right: -1,
              width: 10,
              height: 10,
              borderRadius: '50%',
              bgcolor: isConnected ? '#10B981' : '#F59E0B',
              border: '2px solid #ffffff',
            }}
          />
        </Box>

        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Tooltip title={nickname} arrow placement="top">
            <Typography
              variant="body1"
              sx={{
                fontWeight: 700,
                color: '#09131F',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
                lineHeight: 1.2,
              }}
            >
              {nickname}
            </Typography>
          </Tooltip>
          {!isConnected && (
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', mt: 0.25 }}>
              <WifiOffIcon sx={{ fontSize: '0.75rem', color: '#D97706' }} />
              <Typography variant="caption" sx={{ color: '#D97706', fontWeight: 600 }}>
                offline
              </Typography>
            </Stack>
          )}
        </Box>
      </Stack>

      <Tooltip title={`Remove ${nickname}`}>
        <IconButton
          size="small"
          className="remove-btn"
          disabled={disabled}
          onClick={() => onRemove(id, nickname)}
          aria-label={`Remove ${nickname}`}
          sx={{
            ml: 1,
            color: '#94A3B8',
            opacity: { xs: 1, sm: 0.4 },
            transition: 'all 0.15s ease',
            '&:hover': {
              color: '#DC2626',
              bgcolor: 'rgba(220, 38, 38, 0.1)',
              opacity: 1,
            },
          }}
        >
          <CloseIcon fontSize="small" />
        </IconButton>
      </Tooltip>
    </Box>
  );
});

export default ParticipantTile;
