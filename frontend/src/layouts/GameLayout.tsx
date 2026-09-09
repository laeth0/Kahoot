import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import FullscreenIcon from '@mui/icons-material/Fullscreen';
import FullscreenExitIcon from '@mui/icons-material/FullscreenExit';
import {
  AppBar,
  Box,
  Button,
  Container,
  IconButton,
  Stack,
  Toolbar,
  Tooltip,
  Typography,
} from '@mui/material';
import { type ReactNode, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import logo from '../assets/logo.jpeg';
import { ConfirmDialog } from '../components/ConfirmDialog/index.ts';

export interface GameLayoutProps {
  children: ReactNode;
  quizTitle?: string;
  gamePin?: string;
  isGameActive?: boolean;
}

export function GameLayout({
  children,
  quizTitle,
  gamePin,
  isGameActive = false,
}: GameLayoutProps) {
  const navigate = useNavigate();
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [showExitConfirm, setShowExitConfirm] = useState(false);

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(Boolean(document.fullscreenElement));
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
    };
  }, []);

  const toggleFullscreen = async () => {
    try {
      if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
      } else {
        await document.exitFullscreen();
      }
    } catch {
      return;
    }
  };

  const handleBack = () => {
    if (isGameActive) {
      setShowExitConfirm(true);
    } else {
      navigate('/host/quizzes');
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        bgcolor: '#F4F8FC',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <AppBar
        position="sticky"
        elevation={0}
        sx={{
          bgcolor: '#ffffff',
          borderBottom: '1px solid #E2E8F0',
          color: '#09131F',
        }}
      >
        <Container maxWidth="xl">
          <Toolbar disableGutters sx={{ justifyContent: 'space-between', minHeight: 64 }}>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
              <Button
                startIcon={<ArrowBackIcon />}
                onClick={handleBack}
                variant="text"
                color="inherit"
                sx={{
                  fontWeight: 700,
                  textTransform: 'none',
                  borderRadius: 2,
                }}
              >
                Quizzes
              </Button>

              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', ml: 1 }}>
                <Box
                  component="img"
                  src={logo}
                  alt="IEEEXtreme"
                  sx={{ height: 32, width: 'auto', borderRadius: 1 }}
                />
                {quizTitle && (
                  <Typography
                    variant="h6"
                    sx={{
                      fontWeight: 800,
                      color: '#00629B',
                      display: { xs: 'none', sm: 'block' },
                      maxWidth: { sm: 260, md: 450 },
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                    }}
                  >
                    {quizTitle}
                  </Typography>
                )}
              </Stack>
            </Stack>

            <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
              {gamePin && (
                <Box
                  sx={{
                    display: { xs: 'none', md: 'flex' },
                    alignItems: 'center',
                    gap: 1,
                    px: 2,
                    py: 0.5,
                    bgcolor: 'rgba(0, 98, 155, 0.08)',
                    borderRadius: 2,
                    border: '1px solid rgba(0, 98, 155, 0.2)',
                  }}
                >
                  <Typography
                    variant="caption"
                    sx={{ fontWeight: 700, color: '#00629B', letterSpacing: 0.5 }}
                  >
                    PIN:
                  </Typography>
                  <Typography
                    variant="subtitle2"
                    sx={{ fontWeight: 900, color: '#09131F', fontFamily: 'monospace' }}
                  >
                    {gamePin}
                  </Typography>
                </Box>
              )}

              <Tooltip
                title={isFullscreen ? 'Exit Fullscreen' : 'Enter Fullscreen (Projector Mode)'}
              >
                <IconButton
                  onClick={toggleFullscreen}
                  color="inherit"
                  sx={{
                    border: '1px solid #E2E8F0',
                    borderRadius: 2,
                  }}
                >
                  {isFullscreen ? <FullscreenExitIcon /> : <FullscreenIcon />}
                </IconButton>
              </Tooltip>
            </Stack>
          </Toolbar>
        </Container>
      </AppBar>

      <Box component="main" sx={{ flexGrow: 1, py: { xs: 2.5, md: 4 } }}>
        <Container maxWidth="xl">{children}</Container>
      </Box>

      <ConfirmDialog
        open={showExitConfirm}
        title="Leave Game Session?"
        message="The game will continue running in the background. You can return to it from your Quizzes dashboard at any time."
        confirmLabel="Leave Game"
        confirmColor="primary"
        onConfirm={() => {
          setShowExitConfirm(false);
          navigate('/host/quizzes');
        }}
        onCancel={() => setShowExitConfirm(false)}
      />
    </Box>
  );
}

export default GameLayout;
