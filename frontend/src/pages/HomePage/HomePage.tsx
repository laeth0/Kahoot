import BoltIcon from '@mui/icons-material/Bolt';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import QrCodeScannerIcon from '@mui/icons-material/QrCodeScanner';
import TagIcon from '@mui/icons-material/Tag';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Container,
  Divider,
  InputAdornment,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { type FormEvent, useState } from 'react';

import { gameService } from '../../api/gameService.ts';
import logo from '../../assets/logo.jpeg';

export function HomePage() {
  const [pin, setPin] = useState('');
  const [nickname, setNickname] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [joinSuccess, setJoinSuccess] = useState<string | null>(null);

  const handlePinChange = (value: string) => {
    // Only allow digits and spaces, max 8 chars
    const cleaned = value.replace(/[^0-9\s]/g, '').slice(0, 8);
    setPin(cleaned);
    if (errorMessage) setErrorMessage(null);
  };

  const handleNicknameChange = (value: string) => {
    setNickname(value.slice(0, 30));
    if (errorMessage) setErrorMessage(null);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const rawPin = pin.replace(/\s+/g, '');

    if (!rawPin || rawPin.length < 4) {
      setErrorMessage('Please enter a valid Game PIN (at least 4 digits)');
      return;
    }

    if (!nickname.trim()) {
      setErrorMessage('Please enter your player nickname to join the game');
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      const result = await gameService.joinGame({
        pin: rawPin,
        nickname: nickname.trim(),
      });
      setJoinSuccess(
        `Welcome ${result.nickname}! You joined session #${rawPin}. Waiting for the host to start...`,
      );
    } catch (err) {
      const msg =
        err instanceof Error ? err.message : 'Could not find active game session with that PIN.';
      setErrorMessage(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Box
      sx={{
        flexGrow: 1,
        py: { xs: 4, md: 8 },
        background: 'linear-gradient(180deg, #eef7fc 0%, #f4f8fc 100%)',
        display: 'flex',
        alignItems: 'center',
      }}
    >
      <Container maxWidth="md">
        {/* Main Header / Context */}
        <Stack spacing={2} sx={{ textAlign: 'center', mb: 4, alignItems: 'center' }}>
          <Box
            component="img"
            src={logo}
            alt="IEEEXtreme Palestine Section Emblem"
            sx={{
              width: 88,
              height: 88,
              borderRadius: '50%',
              objectFit: 'cover',
              border: '3px solid #00629b',
              boxShadow:
                '0 10px 15px -3px rgba(0, 98, 155, 0.2), 0 4px 6px -4px rgba(0, 98, 155, 0.1)',
            }}
          />
          <Typography
            variant="h2"
            component="h2"
            sx={{
              fontWeight: 800,
              fontSize: { xs: '2rem', md: '2.5rem' },
              color: '#09131f',
              letterSpacing: '-0.03em',
            }}
          >
            Ready to Compete?
          </Typography>
          <Typography
            variant="body1"
            sx={{
              color: '#334e68',
              maxWidth: 540,
              fontSize: { xs: '0.95rem', md: '1.05rem' },
            }}
          >
            Enter your Game PIN and player handle to join the live IEEEXtreme quiz. No account or
            registration required!
          </Typography>
        </Stack>

        {/* Central Join Card */}
        <Card
          elevation={0}
          sx={{
            maxWidth: 480,
            mx: 'auto',
            borderRadius: 3,
            border: '1px solid #e2e8f0',
            boxShadow:
              '0 20px 25px -5px rgba(9, 19, 31, 0.08), 0 8px 10px -6px rgba(9, 19, 31, 0.04)',
            overflow: 'visible',
          }}
        >
          <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
            {joinSuccess ? (
              <Stack spacing={3} sx={{ textAlign: 'center', py: 2 }}>
                <Alert severity="success" sx={{ fontSize: '1rem', fontWeight: 500 }}>
                  {joinSuccess}
                </Alert>
                <Typography variant="body2" sx={{ color: '#334e68' }}>
                  Keep this window open. Questions will synchronize on your screen as soon as the
                  host launches the game.
                </Typography>
                <Button
                  variant="outlined"
                  color="primary"
                  onClick={() => {
                    setJoinSuccess(null);
                    setPin('');
                    setNickname('');
                  }}
                  sx={{ minHeight: 44 }}
                >
                  Join Different Game
                </Button>
              </Stack>
            ) : (
              <Box component="form" onSubmit={handleSubmit} noValidate>
                {errorMessage && (
                  <Alert severity="error" sx={{ mb: 3 }}>
                    {errorMessage}
                  </Alert>
                )}

                <Stack spacing={3}>
                  {/* Game PIN Field */}
                  <Box>
                    <Typography
                      component="label"
                      htmlFor="game-pin-input"
                      variant="subtitle2"
                      sx={{ fontWeight: 600, color: '#09131f', mb: 1, display: 'block' }}
                    >
                      Game PIN
                    </Typography>
                    <TextField
                      id="game-pin-input"
                      fullWidth
                      placeholder="e.g. 739 204"
                      value={pin}
                      onChange={(e) => handlePinChange(e.target.value)}
                      required
                      autoFocus
                      disabled={isSubmitting}
                      slotProps={{
                        input: {
                          startAdornment: (
                            <InputAdornment position="start">
                              <TagIcon sx={{ color: '#00629b' }} />
                            </InputAdornment>
                          ),
                          sx: {
                            fontSize: '1.25rem',
                            fontWeight: 700,
                            letterSpacing: '0.08em',
                            minHeight: 52,
                          },
                        },
                      }}
                      helperText="Ask the host for the 6-digit game PIN"
                    />
                  </Box>

                  {/* Player Nickname Field */}
                  <Box>
                    <Typography
                      component="label"
                      htmlFor="nickname-input"
                      variant="subtitle2"
                      sx={{ fontWeight: 600, color: '#09131f', mb: 1, display: 'block' }}
                    >
                      Your Nickname
                    </Typography>
                    <TextField
                      id="nickname-input"
                      fullWidth
                      placeholder="e.g. ExtremeCoder"
                      value={nickname}
                      onChange={(e) => handleNicknameChange(e.target.value)}
                      required
                      disabled={isSubmitting}
                      slotProps={{
                        input: {
                          sx: {
                            fontWeight: 600,
                            minHeight: 50,
                          },
                        },
                      }}
                      helperText="This name will appear on the live leaderboard"
                    />
                  </Box>

                  {/* Submit Action */}
                  <Button
                    type="submit"
                    fullWidth
                    variant="contained"
                    color="primary"
                    size="large"
                    disabled={isSubmitting}
                    startIcon={
                      isSubmitting ? (
                        <CircularProgress size={20} color="inherit" />
                      ) : (
                        <PlayArrowIcon />
                      )
                    }
                    sx={{
                      minHeight: 50,
                      fontSize: '1.05rem',
                      fontWeight: 700,
                      boxShadow: '0 4px 10px rgba(0, 98, 155, 0.25)',
                    }}
                  >
                    {isSubmitting ? 'Connecting...' : 'Enter Game'}
                  </Button>
                </Stack>
              </Box>
            )}
          </CardContent>
        </Card>

        {/* Feature Highlights / Badges */}
        <Paper
          elevation={0}
          sx={{
            mt: 4,
            p: 2.5,
            borderRadius: 2,
            border: '1px solid #e2e8f0',
            bgcolor: '#ffffff',
          }}
        >
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            divider={<Divider orientation="vertical" flexItem />}
            spacing={{ xs: 2, sm: 3 }}
            sx={{ justifyContent: 'space-around', alignItems: 'center' }}
          >
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
              <QrCodeScannerIcon sx={{ color: '#00629b' }} />
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  Instant Join
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  No registration or sign up needed
                </Typography>
              </Box>
            </Stack>

            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
              <BoltIcon sx={{ color: '#0284c7' }} />
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  Live Synchronization
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  Real-time questions & countdowns
                </Typography>
              </Box>
            </Stack>

            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
              <LeaderboardIcon sx={{ color: '#059669' }} />
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  Live Leaderboard
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  Compete for top scores and badges
                </Typography>
              </Box>
            </Stack>
          </Stack>
        </Paper>
      </Container>
    </Box>
  );
}

export default HomePage;
