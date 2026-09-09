import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
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

export interface GamePinDisplayProps {
  pin: string;
  quizTitle?: string;
}

export function GamePinDisplay({ pin, quizTitle }: GamePinDisplayProps) {
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const joinUrl =
    typeof window !== 'undefined'
      ? `${window.location.origin}/join?pin=${pin}`
      : `/join?pin=${pin}`;
  const displayHost =
    typeof window !== 'undefined' ? `${window.location.host}/join` : 'our-site/join';

  const handleCopyPin = async () => {
    try {
      await navigator.clipboard.writeText(pin);
      setToastMessage('Game PIN copied to clipboard!');
    } catch {
      setToastMessage('Failed to copy PIN');
    }
  };

  const handleCopyLink = async () => {
    try {
      await navigator.clipboard.writeText(joinUrl);
      setToastMessage('Join link copied to clipboard!');
    } catch {
      setToastMessage('Failed to copy link');
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
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1.5 }}>
            <Typography
              variant="h6"
              sx={{
                fontWeight: 700,
                color: '#486581',
                fontSize: { xs: '1rem', sm: '1.25rem', md: '1.5rem' },
              }}
            >
              Join at
            </Typography>
            <Box
              sx={{
                bgcolor: 'rgba(2, 132, 199, 0.1)',
                color: '#0284C7',
                px: 2,
                py: 0.5,
                borderRadius: 2,
                fontWeight: 800,
                fontSize: { xs: '1.1rem', sm: '1.35rem', md: '1.6rem' },
                letterSpacing: 0.5,
              }}
            >
              {displayHost}
            </Box>
          </Stack>

          <Typography
            variant="body2"
            sx={{ color: '#627D98', fontWeight: 500, mb: { xs: 2, sm: 3 } }}
          >
            or enter the game PIN below on your phone or laptop
          </Typography>

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
        open={Boolean(toastMessage)}
        autoHideDuration={3000}
        onClose={() => setToastMessage(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={() => setToastMessage(null)}
          severity="success"
          variant="filled"
          icon={<CheckCircleOutlineIcon fontSize="inherit" />}
          sx={{ width: '100%', borderRadius: 2, fontWeight: 600 }}
        >
          {toastMessage}
        </Alert>
      </Snackbar>
    </>
  );
}

export default GamePinDisplay;
