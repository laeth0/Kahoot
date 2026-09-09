import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import { Alert, Box, Card, CardContent, Container, Stack, Typography } from '@mui/material';
import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

import { ApiError } from '../../api/axiosClient.ts';
import { gameService } from '../../api/gameService.ts';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { NicknameEntryForm } from '../../components/NicknameEntryForm/index.ts';
import { PinEntryForm } from '../../components/PinEntryForm/index.ts';
import { getFriendlyErrorMessage } from '../../constants/errorCodes.ts';
import { saveSession } from '../../hooks/useSessionToken.ts';

type JoinStep = 'pin' | 'nickname';

const SIX_DIGIT_PIN = /^\d{6}$/;
const RATE_LIMIT_COOLDOWN_MS = 4000;

export function JoinPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const initialPin = (searchParams.get('pin') ?? '').trim();

  const [pin, setPin] = useState(initialPin);
  const [step, setStep] = useState<JoinStep>(() =>
    SIX_DIGIT_PIN.test(initialPin) ? 'nickname' : 'pin',
  );
  const [pinError, setPinError] = useState<string | null>(null);
  const [nicknameServerError, setNicknameServerError] = useState<string | null>(null);
  const [generalError, setGeneralError] = useState<string | null>(null);
  const [isJoining, setIsJoining] = useState(false);
  const [isCoolingDown, setIsCoolingDown] = useState(false);

  const cooldownTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (cooldownTimeoutRef.current) {
        clearTimeout(cooldownTimeoutRef.current);
      }
    },
    [],
  );

  const clearErrors = () => {
    setPinError(null);
    setNicknameServerError(null);
    setGeneralError(null);
  };

  const handlePinContinue = (enteredPin: string) => {
    setPin(enteredPin);
    setSearchParams({ pin: enteredPin }, { replace: true });
    clearErrors();
    setStep('nickname');
  };

  const handleChangePin = () => {
    setNicknameServerError(null);
    setGeneralError(null);
    setStep('pin');
  };

  const startCooldown = () => {
    setIsCoolingDown(true);
    if (cooldownTimeoutRef.current) {
      clearTimeout(cooldownTimeoutRef.current);
    }
    cooldownTimeoutRef.current = setTimeout(() => {
      setIsCoolingDown(false);
    }, RATE_LIMIT_COOLDOWN_MS);
  };

  const handleJoin = async (nickname: string) => {
    setIsJoining(true);
    clearErrors();

    try {
      const response = await gameService.joinGame({ pin, nickname });
      saveSession(response.gameId, {
        sessionToken: response.sessionToken,
        participantId: response.participantId,
        nickname: response.nickname,
        gameId: response.gameId,
      });
      navigate(`/play/${response.gameId}`);
    } catch (err) {
      const code = err instanceof ApiError ? err.code : undefined;
      const status = err instanceof ApiError ? err.status : undefined;
      const message =
        err instanceof Error ? err.message : 'Unable to join the game. Please try again.';

      if (code === 'Game.InvalidPin' || status === 404) {
        setPinError(getFriendlyErrorMessage('Game.InvalidPin'));
        setStep('pin');
      } else if (code === 'Game.NicknameTaken') {
        setNicknameServerError(getFriendlyErrorMessage('Game.NicknameTaken'));
      } else if (code === 'Game.NotJoinable') {
        setGeneralError(getFriendlyErrorMessage('Game.NotJoinable'));
      } else if (status === 429) {
        setGeneralError(message);
        startCooldown();
      } else {
        setGeneralError(message);
      }
    } finally {
      setIsJoining(false);
    }
  };

  return (
    <>
      <MetadataManager
        title="Enter Game"
        description="Join a live IEEEXtreme quiz session with your Game PIN and nickname."
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
          {step === 'pin' ? (
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
                  <Typography
                    variant="h4"
                    component="h1"
                    sx={{ fontWeight: 800, color: '#09131f' }}
                  >
                    Join Game
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#486581' }}>
                    Enter the 6-digit PIN shown on the host&apos;s screen
                  </Typography>
                </Stack>

                {pinError && (
                  <Alert severity="error" sx={{ mb: 3 }}>
                    {pinError}
                  </Alert>
                )}

                <PinEntryForm
                  initialPin={pin}
                  onSubmit={handlePinContinue}
                  buttonText="Continue"
                  helperText="Enter the 6-digit game PIN"
                />
              </CardContent>
            </Card>
          ) : (
            <Stack spacing={2}>
              {generalError && <Alert severity="error">{generalError}</Alert>}
              <NicknameEntryForm
                pin={pin}
                isLoading={isJoining || isCoolingDown}
                serverError={nicknameServerError}
                onSubmit={handleJoin}
                onBack={handleChangePin}
              />
            </Stack>
          )}
        </Container>
      </Box>
    </>
  );
}

export default JoinPage;
