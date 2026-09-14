import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import LinkIcon from '@mui/icons-material/Link';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  IconButton,
  Snackbar,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import { useState } from 'react';

import { copyToClipboard } from '../../utils/clipboard.ts';

export interface GamePinDisplayProps {
  pin: string;
  quizTitle?: string;
  joinUrl?: string;
}

export function GamePinDisplay({ pin, quizTitle, joinUrl }: GamePinDisplayProps) {
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(
    null,
  );

  const effectiveJoinUrl =
    joinUrl ||
    (typeof window !== 'undefined'
      ? `${window.location.origin}/join?pin=${pin}`
      : `/join?pin=${pin}`);

  const handleCopyPin = async () => {
    console.info('[GameLobby] Copy PIN action triggered', {
      environment: import.meta.env.MODE,
      pin,
      currentOrigin: typeof window !== 'undefined' ? window.location.origin : 'N/A',
      isSecureContext: typeof window !== 'undefined' ? window.isSecureContext : false,
    });

    const result = await copyToClipboard(pin, 'Game PIN');
    if (result.success) {
      setToast({ message: 'Game PIN copied to clipboard!', severity: 'success' });
    } else {
      setToast({
        message: `Failed to copy PIN. PIN: ${pin}`,
        severity: 'error',
      });
    }
  };

  const handleCopyLink = async () => {
    console.info('[GameLobby] Copy Join Link action triggered', {
      environment: import.meta.env.MODE,
      generatedJoinUrl: effectiveJoinUrl,
      currentOrigin: typeof window !== 'undefined' ? window.location.origin : 'N/A',
      apiBaseUrl: import.meta.env.VITE_API_URL ?? '/api',
      urlSource: joinUrl ? 'Server Configuration' : 'Client Fallback',
      isSecureContext: typeof window !== 'undefined' ? window.isSecureContext : false,
    });

    const result = await copyToClipboard(effectiveJoinUrl, 'Join Link');
    if (result.success) {
      setToast({ message: 'Join link copied to clipboard!', severity: 'success' });
    } else {
      setToast({
        message: `Failed to copy link. URL: ${effectiveJoinUrl}`,
        severity: 'error',
      });
    }
  };

  return (
    <>
      <Card
        sx={{
          borderRadius: 4,
          boxShadow: '0 12px 32px rgba(0, 98, 155, 0.12)',
          border: '2px solid #E2E8F0',
          overflow: 'hidden',
          bgcolor: '#ffffff',
          position: 'relative',
        }}
      >
        <Box
          sx={{
            bgcolor: '#00629B',
            color: '#ffffff',
            py: 1.5,
            px: 3,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 1,
          }}
        >
          <Typography variant="subtitle1" sx={{ fontWeight: 700, letterSpacing: 0.5 }}>
            {quizTitle ? quizTitle : 'Live Quiz Session'}
          </Typography>
          <Typography
            variant="caption"
            sx={{
              bgcolor: 'rgba(255, 255, 255, 0.2)',
              px: 1.5,
              py: 0.5,
              borderRadius: 2,
              fontWeight: 700,
              textTransform: 'uppercase',
              letterSpacing: 1,
            }}
          >
            Lobby Waiting Room
          </Typography>
        </Box>

        <CardContent
          sx={{
            p: { xs: 3, sm: 4, md: 5 },
            textAlign: 'center',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
          }}
        >
          <Box
            onClick={handleCopyPin}
            sx={{
              my: { xs: 1, sm: 2 },
              px: { xs: 3, sm: 6 },
              py: { xs: 1.5, sm: 2 },
              borderRadius: 4,
              bgcolor: '#F4F8FC',
              border: '3px dashed #00629B',
              cursor: 'pointer',
              transition: 'all 0.2s ease',
              '&:hover': {
                bgcolor: 'rgba(0, 98, 155, 0.05)',
                borderColor: '#0284C7',
                transform: 'scale(1.02)',
              },
              display: 'inline-flex',
              flexDirection: 'column',
              alignItems: 'center',
            }}
          >
            <Typography
              variant="caption"
              sx={{
                fontWeight: 700,
                color: '#00629B',
                letterSpacing: 1.5,
                textTransform: 'uppercase',
                mb: 0.5,
              }}
            >
              Game PIN
            </Typography>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
              <Typography
                variant="h1"
                sx={{
                  fontWeight: 900,
                  color: '#09131F',
                  fontFamily: 'monospace',
                  letterSpacing: { xs: 6, sm: 10, md: 14 },
                  fontSize: { xs: '2.75rem', sm: '4rem', md: '5.25rem' },
                  userSelect: 'all',
                  lineHeight: 1.1,
                }}
              >
                {pin}
              </Typography>
              <Tooltip title="Copy PIN">
                <IconButton
                  size="large"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleCopyPin();
                  }}
                  sx={{
                    color: '#00629B',
                    bgcolor: 'rgba(0, 98, 155, 0.1)',
                    '&:hover': { bgcolor: 'rgba(0, 98, 155, 0.2)' },
                  }}
                  aria-label="Copy Game PIN"
                >
                  <ContentCopyIcon fontSize="medium" />
                </IconButton>
              </Tooltip>
            </Stack>
          </Box>

          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={2}
            sx={{ mt: { xs: 3, sm: 4 }, width: '100%', maxWidth: 440, justifyContent: 'center' }}
          >
            <Button
              variant="outlined"
              color="primary"
              onClick={handleCopyPin}
              startIcon={<ContentCopyIcon />}
              sx={{
                flex: 1,
                minHeight: 46,
                fontWeight: 700,
                borderRadius: 3,
                textTransform: 'none',
              }}
            >
              Copy PIN
            </Button>
            <Button
              variant="contained"
              color="primary"
              onClick={handleCopyLink}
              startIcon={<LinkIcon />}
              sx={{
                flex: 1,
                minHeight: 46,
                fontWeight: 700,
                borderRadius: 3,
                textTransform: 'none',
                bgcolor: '#00629B',
                '&:hover': { bgcolor: '#004f7d' },
              }}
            >
              Copy Join Link
            </Button>
          </Stack>
        </CardContent>
      </Card>

      <Snackbar
        open={Boolean(toast)}
        autoHideDuration={3000}
        onClose={() => setToast(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={() => setToast(null)}
          severity={toast?.severity ?? 'success'}
          variant="filled"
          icon={toast?.severity === 'success' ? <CheckCircleIcon fontSize="inherit" /> : undefined}
          sx={{ width: '100%', borderRadius: 2, fontWeight: 600 }}
        >
          {toast?.message}
        </Alert>
      </Snackbar>
    </>
  );
}

export default GamePinDisplay;
