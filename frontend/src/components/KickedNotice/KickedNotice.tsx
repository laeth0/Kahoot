import HomeIcon from '@mui/icons-material/Home';
import PersonRemoveIcon from '@mui/icons-material/PersonRemove';
import { Box, Button, Paper, Stack, Typography } from '@mui/material';

export interface KickedNoticeProps {
  onHome: () => void;
}

export function KickedNotice({ onHome }: KickedNoticeProps) {
  return (
    <Paper
      elevation={0}
      role="alert"
      sx={{
        borderRadius: 4,
        border: '1px solid #FECDD3',
        bgcolor: '#FFFFFF',
        p: { xs: 3, sm: 4 },
        textAlign: 'center',
        boxShadow: '0 20px 25px -5px rgba(239, 68, 68, 0.12)',
      }}
    >
      <Stack spacing={2.5} sx={{ alignItems: 'center', maxWidth: 420, mx: 'auto' }}>
        <Box
          sx={{
            width: 64,
            height: 64,
            borderRadius: '50%',
            bgcolor: '#FEF2F2',
            color: '#EF4444',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <PersonRemoveIcon sx={{ fontSize: 34 }} />
        </Box>

        <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131F' }}>
          You&apos;ve been removed
        </Typography>

        <Typography variant="body1" sx={{ color: '#486581', lineHeight: 1.6 }}>
          You have been removed from this game by the host.
        </Typography>

        <Typography variant="body2" sx={{ color: '#64748B' }}>
          You cannot rejoin this game session with the same nickname.
        </Typography>

        <Button
          onClick={onHome}
          variant="contained"
          color="primary"
          startIcon={<HomeIcon />}
          sx={{ minHeight: 48, px: 3, fontWeight: 700, mt: 1 }}
        >
          Return to Home
        </Button>
      </Stack>
    </Paper>
  );
}

export default KickedNotice;
