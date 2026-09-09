import RefreshIcon from '@mui/icons-material/Refresh';
import SyncIcon from '@mui/icons-material/Sync';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import { Alert, Box, Button, CircularProgress, Stack, Typography } from '@mui/material';

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

  return (
    <Box sx={{ width: '100%', mb: 2 }}>
      <Alert
        severity={isDisconnected ? 'error' : 'warning'}
        icon={
          isDisconnected ? (
            <WifiOffIcon fontSize="inherit" />
          ) : isReconnecting ? (
            <SyncIcon
              fontSize="inherit"
              sx={{
                animation: 'spin 2s linear infinite',
                '@keyframes spin': {
                  '0%': { transform: 'rotate(0deg)' },
                  '100%': { transform: 'rotate(360deg)' },
                },
              }}
            />
          ) : (
            <WarningAmberIcon fontSize="inherit" />
          )
        }
        action={
          isDisconnected && onRetry ? (
            <Button
              color="inherit"
              size="small"
              variant="outlined"
              onClick={onRetry}
              startIcon={<RefreshIcon />}
              sx={{ fontWeight: 600, textTransform: 'none' }}
            >
              Reconnect
            </Button>
          ) : null
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
            {isDisconnected && 'Disconnected from live game hub. Real-time updates paused.'}
          </Typography>
        </Stack>
      </Alert>
    </Box>
  );
}

export default ConnectionStatusBanner;
