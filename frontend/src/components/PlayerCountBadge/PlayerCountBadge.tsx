import PeopleAltIcon from '@mui/icons-material/PeopleAlt';
import { Box, Chip, Stack, Typography } from '@mui/material';

export interface PlayerCountBadgeProps {
  count: number;
  variant?: 'compact' | 'projector';
}

export function PlayerCountBadge({ count, variant = 'projector' }: PlayerCountBadgeProps) {
  const isProjector = variant === 'projector';

  if (!isProjector) {
    return (
      <Chip
        icon={<PeopleAltIcon />}
        label={`${count} ${count === 1 ? 'Player' : 'Players'}`}
        color="primary"
        variant="outlined"
        size="medium"
        sx={{ fontWeight: 700, borderRadius: 2 }}
      />
    );
  }

  return (
    <Box
      sx={{
        display: 'inline-flex',
        alignItems: 'center',
        px: { xs: 2, sm: 3 },
        py: { xs: 1, sm: 1.5 },
        bgcolor: '#ffffff',
        border: '2px solid #00629B',
        borderRadius: 4,
        boxShadow: '0 8px 24px rgba(0, 98, 155, 0.12)',
        transition: 'all 0.2s ease-in-out',
      }}
    >
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: { xs: 36, sm: 44 },
            height: { xs: 36, sm: 44 },
            borderRadius: '50%',
            bgcolor: 'rgba(0, 98, 155, 0.1)',
            color: '#00629B',
          }}
        >
          <PeopleAltIcon sx={{ fontSize: { xs: 22, sm: 26 } }} />
        </Box>
        <Box>
          <Typography
            variant="caption"
            sx={{
              display: 'block',
              fontWeight: 700,
              textTransform: 'uppercase',
              letterSpacing: 1,
              color: '#486581',
              fontSize: { xs: '0.7rem', sm: '0.75rem' },
            }}
          >
            Players Joined
          </Typography>
          <Typography
            variant="h4"
            sx={{
              fontWeight: 900,
              color: '#09131F',
              lineHeight: 1,
              fontSize: { xs: '1.5rem', sm: '2rem' },
            }}
          >
            {count}
          </Typography>
        </Box>
      </Stack>
    </Box>
  );
}

export default PlayerCountBadge;
