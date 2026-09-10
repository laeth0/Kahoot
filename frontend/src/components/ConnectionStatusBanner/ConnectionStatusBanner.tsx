import RefreshIcon from '@mui/icons-material/Refresh';
import SyncIcon from '@mui/icons-material/Sync';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import { Alert, Box, Button, CircularProgress, Paper, Stack, Typography } from '@mui/material';

import type { HubConnectionStatus } from '../../hooks/useGameHubConnection.ts';

export interface ConnectionStatusBannerProps {
  status: HubConnectionStatus;
  onRetry?: () => void;
}

export function ConnectionStatusBanner({ status, onRetry }: ConnectionStatusBannerProps) {
  if (status === 'connected') {
    return null;
  }

  const isReconnecting = status === 'reconnecting';
  const isConnecting = status === 'connecting';
  const isDisconnected = status === 'disconnected';

  if (isDisconnected) {
    return (
      <Box
        role="dialog"
        aria-modal="true"
        aria-labelledby="disconnected-modal-title"
        aria-describedby="disconnected-modal-desc"
        sx={{
          position: 'fixed',
          inset: 0,
          zIndex: 1400,
          bgcolor: 'rgba(9, 19, 31, 0.65)',
          backdropFilter: 'blur(4px)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          p: 3,
        }}
      >
        <Paper
          elevation={6}
          sx={{
            maxWidth: 480,
            width: '100%',
            p: { xs: 3, sm: 4 },
            borderRadius: 4,
            border: '2px solid #EF4444',
            textAlign: 'center',
            bgcolor: '#FFFFFF',
          }}
        >
          <Box
            sx={{
              width: 64,
              height: 64,
              borderRadius: '50%',
              bgcolor: '#FEE2E2',
              color: '#DC2626',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              mb: 2.5,
            }}
          >
            <WifiOffIcon sx={{ fontSize: 36 }} />
          </Box>
          <Typography
            id="disconnected-modal-title"
            variant="h5"
            component="h2"
            sx={{ fontWeight: 800, color: '#09131F', mb: 1 }}
          >
            Live Connection Lost
          </Typography>
          <Typography
            id="disconnected-modal-desc"
            variant="body1"
            sx={{ color: '#486581', mb: 3.5, lineHeight: 1.6 }}
          >
            Real-time connection to the live game server was dropped. Countdowns are paused. Tap
            reconnect to restore your live session.
          </Typography>
          {onRetry && (
            <Button
              variant="contained"
              color="error"
              size="large"
              fullWidth
              onClick={onRetry}
              startIcon={<RefreshIcon />}
              sx={{ minHeight: 48, fontWeight: 700, borderRadius: 2 }}
            >
              Reconnect Now
            </Button>
          )}
        </Paper>
      </Box>
    );
  }

  return (
    <Box sx={{ width: '100%', mb: 2 }}>
      <Alert
        severity="warning"
        icon={
          isReconnecting ? (
            <SyncIcon
              fontSize="inherit"
              sx={{
                animation: 'spin 2s linear infinite',
                '@keyframes spin': {
                  '0%': { transform: 'rotate(0deg)' },
                  '100%': { transform: 'rotate(360deg)' },
                },
                '@media (prefers-reduced-motion: reduce)': {
                  animation: 'none',
                },
              }}
            />
          ) : (
            <WarningAmberIcon fontSize="inherit" />
          )
        }
        sx={{
          alignItems: 'center',
          borderRadius: 2,
          fontWeight: 500,
          boxShadow: '0 2px 8px rgba(0,0,0,0.06)',
        }}
      >
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
          {(isConnecting || isReconnecting) && <CircularProgress size={16} color="inherit" />}
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {isConnecting && 'Connecting to real-time game hub...'}
            {isReconnecting && 'Connection interrupted. Reconnecting to live game hub...'}
          </Typography>
        </Stack>
      </Alert>
    </Box>
  );
}

export default ConnectionStatusBanner;
