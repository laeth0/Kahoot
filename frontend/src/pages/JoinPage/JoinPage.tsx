import PersonIcon from '@mui/icons-material/Person';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Container,
  InputAdornment,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { type FormEvent, useId, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import { gameService } from '../../api/gameService.ts';
import { InlineFieldError } from '../../components/Feedback/InlineFieldError.tsx';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { PinEntryForm } from '../../components/PinEntryForm/index.ts';
import { getFriendlyErrorMessage } from '../../constants/errorCodes.ts';
import { VALIDATION } from '../../constants/validation.ts';

export function JoinPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialPin = searchParams.get('pin') ?? '';

  const nicknameErrorId = useId();

  const [pin, setPin] = useState(initialPin);
  const [nickname, setNickname] = useState('');
  const [nicknameError, setNicknameError] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [isJoining, setIsJoining] = useState(false);
  const [joinResult, setJoinResult] = useState<{
    gameId: string;
    participantId: string;
    nickname: string;
  } | null>(null);

  const hasValidPin = pin.trim().length >= VALIDATION.PIN.MIN_LENGTH;

  const handlePinSubmit = (enteredPin: string) => {
    setPin(enteredPin);
    setSearchParams({ pin: enteredPin });
    setApiError(null);
  };

  const handleNicknameChange = (value: string) => {
    setNickname(value.slice(0, VALIDATION.NICKNAME.MAX_LENGTH));
    if (nicknameError) setNicknameError(null);
    if (apiError) setApiError(null);
  };

  const handleJoinSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const cleanNickname = nickname.trim();

    if (!cleanNickname || cleanNickname.length < VALIDATION.NICKNAME.MIN_LENGTH) {
      setNicknameError(
        `Nickname must be between ${VALIDATION.NICKNAME.MIN_LENGTH} and ${VALIDATION.NICKNAME.MAX_LENGTH} characters`,
      );
      return;
    }

    setIsJoining(true);
    setApiError(null);

    try {
      const response = await gameService.joinGame({
        pin: pin.trim(),
        nickname: cleanNickname,
      });

      sessionStorage.setItem(`kahoot_session_${response.gameId}`, response.sessionToken);

      setJoinResult({
        gameId: response.gameId,
        participantId: response.participantId,
        nickname: response.nickname,
      });
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Unable to join game';
      setApiError(getFriendlyErrorMessage(msg));
    } finally {
      setIsJoining(false);
    }
  };

  return (
    <>
      <MetadataManager
        title="Enter Game | Join Session"
        description="Join a live IEEEXtreme quiz session with your Game PIN and player handle."
      />

      <Box
        sx={{
          flexGrow: 1,
          py: { xs: 4, sm: 8 },
          background: 'linear-gradient(180deg, #eef7fc 0%, #f4f8fc 100%)',
          display: 'flex',
          alignItems: 'center',
        }}
      >
        <Container maxWidth="sm">
          <Card
            elevation={0}
            sx={{
              borderRadius: 3,
              border: '1px solid #e2e8f0',
              boxShadow:
                '0 20px 25px -5px rgba(9, 19, 31, 0.08), 0 8px 10px -6px rgba(9, 19, 31, 0.04)',
            }}
          >
            <CardContent sx={{ p: { xs: 3, sm: 4.5 } }}>
              <Stack spacing={1.5} sx={{ textAlign: 'center', mb: 3.5, alignItems: 'center' }}>
                <Box
                  sx={{
                    width: 56,
                    height: 56,
                    borderRadius: '50%',
                    bgcolor: '#eef7fc',
                    color: '#00629b',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <SportsEsportsIcon sx={{ fontSize: 32 }} />
                </Box>
                <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131f' }}>
                  {joinResult ? 'You are In!' : hasValidPin ? 'Enter Your Nickname' : 'Join Game'}
                </Typography>
                <Typography variant="body2" sx={{ color: '#334e68' }}>
                  {joinResult
                    ? 'Connected to the live game lobby. Keep this screen open!'
                    : hasValidPin
                      ? `Joining Session PIN: ${pin}`
                      : 'Enter the session PIN from the host'}
                </Typography>
              </Stack>

              {joinResult ? (
                <Stack spacing={3} sx={{ textAlign: 'center', py: 2 }}>
                  <Alert severity="success" sx={{ fontSize: '1.05rem', fontWeight: 600 }}>
                    Welcome, {joinResult.nickname}!
                  </Alert>
                  <Typography variant="body2" sx={{ color: '#334e68', lineHeight: 1.6 }}>
                    Waiting for the host to launch the first question. Your screen will synchronize
                    instantly when the quiz starts.
                  </Typography>
                  <Button
                    variant="outlined"
                    color="primary"
                    onClick={() => {
                      setJoinResult(null);
                      setPin('');
                      setNickname('');
                      setSearchParams({});
                    }}
                    sx={{ minHeight: 44, fontWeight: 600 }}
                  >
                    Join a Different Game
                  </Button>
                </Stack>
              ) : !hasValidPin ? (
                <PinEntryForm
                  initialPin={pin}
                  onSubmit={handlePinSubmit}
                  buttonText="Continue"
                  helperText="Enter the 6-digit game PIN"
                />
              ) : (
                <Box component="form" onSubmit={handleJoinSubmit} noValidate>
                  {apiError && (
                    <Alert severity="error" sx={{ mb: 3 }}>
                      {apiError}
                    </Alert>
                  )}

                  <Stack spacing={3}>
                    <Box>
                      <Typography
                        component="label"
                        htmlFor="nickname-field"
                        variant="subtitle2"
                        sx={{ fontWeight: 600, color: '#09131f', mb: 1, display: 'block' }}
                      >
                        Player Handle / Nickname
                      </Typography>
                      <TextField
                        id="nickname-field"
                        fullWidth
                        placeholder="e.g. ExtremeCoder"
                        value={nickname}
                        onChange={(e) => handleNicknameChange(e.target.value)}
                        required
                        autoFocus
                        disabled={isJoining}
                        error={Boolean(nicknameError)}
                        aria-describedby={nicknameError ? nicknameErrorId : undefined}
                        slotProps={{
                          input: {
                            startAdornment: (
                              <InputAdornment position="start">
                                <PersonIcon sx={{ color: '#00629b' }} />
                              </InputAdornment>
                            ),
                            sx: {
                              fontWeight: 600,
                              minHeight: 52,
                              fontSize: '1.1rem',
                            },
                          },
                        }}
                        helperText={
                          !nicknameError
                            ? 'This handle will be displayed on the live podium'
                            : undefined
                        }
                      />
                      <InlineFieldError id={nicknameErrorId} error={nicknameError} />
                    </Box>

                    <Button
                      type="submit"
                      fullWidth
                      variant="contained"
                      color="primary"
                      size="large"
                      disabled={isJoining}
                      startIcon={
                        isJoining ? (
                          <CircularProgress size={20} color="inherit" />
                        ) : (
                          <PlayArrowIcon />
                        )
                      }
                      sx={{
                        minHeight: 52,
                        fontSize: '1.05rem',
                        fontWeight: 700,
                        borderRadius: 2,
                        boxShadow: '0 4px 12px rgba(0, 98, 155, 0.25)',
                      }}
                    >
                      {isJoining ? 'Joining Game...' : 'Join Game Now'}
                    </Button>

                    <Button
                      variant="text"
                      color="inherit"
                      onClick={() => {
                        setPin('');
                        setSearchParams({});
                      }}
                      sx={{ color: '#334e68', fontSize: '0.9rem' }}
                    >
                      ← Change Game PIN
                    </Button>
                  </Stack>
                </Box>
              )}
            </CardContent>
          </Card>
        </Container>
      </Box>
    </>
  );
}

export default JoinPage;
