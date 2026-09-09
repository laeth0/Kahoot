import PeopleOutlineIcon from '@mui/icons-material/PeopleOutline';
import SearchIcon from '@mui/icons-material/Search';
import { Box, Card, InputAdornment, Stack, TextField, Typography } from '@mui/material';
import { useMemo, useState } from 'react';

import type { GameParticipantResponse } from '../../realtime/events.ts';
import { ConfirmDialog } from '../ConfirmDialog/index.ts';
import { ParticipantTile } from '../ParticipantTile/index.ts';

export interface ParticipantGridProps {
  participants: GameParticipantResponse[];
  onRemoveParticipant: (participantId: string) => Promise<void> | void;
  isRemoving?: boolean;
}

const MAX_DISPLAY_COUNT = 150;

export function ParticipantGrid({
  participants,
  onRemoveParticipant,
  isRemoving = false,
}: ParticipantGridProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [kickTarget, setKickTarget] = useState<{ id: string; nickname: string } | null>(null);

  const filteredParticipants = useMemo(() => {
    if (!searchQuery.trim()) {
      return participants;
    }
    const query = searchQuery.trim().toLowerCase();
    return participants.filter((p) => p.nickname.toLowerCase().includes(query));
  }, [participants, searchQuery]);

  const displayedParticipants = useMemo(() => {
    if (filteredParticipants.length <= MAX_DISPLAY_COUNT) {
      return filteredParticipants;
    }
    return filteredParticipants.slice(0, MAX_DISPLAY_COUNT);
  }, [filteredParticipants]);

  const handleConfirmKick = async () => {
    if (!kickTarget) return;
    try {
      await onRemoveParticipant(kickTarget.id);
    } finally {
      setKickTarget(null);
    }
  };

  return (
    <Box sx={{ width: '100%', mt: 3 }}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{
          alignItems: { xs: 'stretch', sm: 'center' },
          justifyContent: 'space-between',
          mb: 2.5,
        }}
      >
        <Typography variant="h6" sx={{ fontWeight: 800, color: '#09131F' }}>
          Participants ({participants.length})
        </Typography>

        {participants.length > 5 && (
          <TextField
            size="small"
            placeholder="Search players..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon sx={{ color: '#94A3B8', fontSize: 20 }} />
                  </InputAdornment>
                ),
              },
            }}
            sx={{
              maxWidth: { xs: '100%', sm: 260 },
              bgcolor: '#ffffff',
              borderRadius: 2,
              '& .MuiOutlinedInput-root': {
                borderRadius: 2,
              },
            }}
          />
        )}
      </Stack>

      {participants.length === 0 ? (
        <Card
          sx={{
            py: 8,
            px: 3,
            textAlign: 'center',
            bgcolor: '#ffffff',
            borderRadius: 4,
            border: '2px dashed #CBD5E1',
            boxShadow: 'none',
          }}
        >
          <Box
            sx={{
              display: 'inline-flex',
              p: 2,
              borderRadius: '50%',
              bgcolor: 'rgba(0, 98, 155, 0.08)',
              color: '#00629B',
              mb: 2,
            }}
          >
            <PeopleOutlineIcon sx={{ fontSize: 48 }} />
          </Box>
          <Typography variant="h5" sx={{ fontWeight: 800, color: '#09131F', mb: 1 }}>
            Waiting for players to join...
          </Typography>
          <Typography variant="body1" sx={{ color: '#64748B', maxWidth: 460, mx: 'auto' }}>
            Share the Game PIN or join link on the projector. Players will appear here instantly as
            they join.
          </Typography>
        </Card>
      ) : (
        <>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: {
                xs: 'repeat(1, 1fr)',
                sm: 'repeat(2, 1fr)',
                md: 'repeat(3, 1fr)',
                lg: 'repeat(4, 1fr)',
              },
              gap: 1.5,
            }}
          >
            {displayedParticipants.map((participant) => (
              <Box
                key={participant.id}
                sx={{
                  contentVisibility: 'auto',
                  containIntrinsicSize: '58px',
                }}
              >
                <ParticipantTile
                  participant={participant}
                  onRemove={(id, nickname) => setKickTarget({ id, nickname })}
                  disabled={isRemoving}
                />
              </Box>
            ))}
          </Box>

          {filteredParticipants.length > MAX_DISPLAY_COUNT && (
            <Box
              sx={{
                mt: 3,
                p: 2,
                textAlign: 'center',
                bgcolor: 'rgba(0, 98, 155, 0.05)',
                borderRadius: 3,
                border: '1px solid #BAE6FD',
              }}
            >
              <Typography variant="body2" sx={{ fontWeight: 600, color: '#00629B' }}>
                Showing first {MAX_DISPLAY_COUNT} of {filteredParticipants.length} players. Use the
                search box above to quickly find specific participants.
              </Typography>
            </Box>
          )}

          {filteredParticipants.length === 0 && searchQuery.trim() !== '' && (
            <Box sx={{ py: 6, textAlign: 'center' }}>
              <Typography variant="body1" sx={{ color: '#64748B', fontWeight: 600 }}>
                No players found matching &quot;{searchQuery}&quot;
              </Typography>
            </Box>
          )}
        </>
      )}

      <ConfirmDialog
        open={Boolean(kickTarget)}
        title="Remove Participant?"
        message={
          kickTarget
            ? `Are you sure you want to remove "${kickTarget.nickname}" from this game? They will not be able to rejoin with the same nickname.`
            : ''
        }
        confirmLabel="Remove Player"
        confirmColor="error"
        isConfirming={isRemoving}
        onConfirm={handleConfirmKick}
        onCancel={() => setKickTarget(null)}
      />
    </Box>
  );
}

export default ParticipantGrid;
