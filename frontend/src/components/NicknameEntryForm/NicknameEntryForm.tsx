import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PersonIcon from '@mui/icons-material/Person';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import {
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  InputAdornment,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { type FormEvent, useId, useState } from 'react';

import { VALIDATION } from '../../constants/validation.ts';
import { InlineFieldError } from '../Feedback/InlineFieldError.tsx';

export interface NicknameEntryFormProps {
  pin: string;
  onSubmit: (nickname: string) => Promise<void> | void;
  isLoading?: boolean;
  serverError?: string | null;
  onBack?: () => void;
}

const { MIN_LENGTH, MAX_LENGTH } = VALIDATION.NICKNAME;

export function NicknameEntryForm({
  pin,
  onSubmit,
  isLoading = false,
  serverError = null,
  onBack,
}: NicknameEntryFormProps) {
  const errorId = useId();
  const [nickname, setNickname] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  const trimmedLength = nickname.trim().length;
  const activeError = localError ?? serverError;

  const handleChange = (value: string) => {
    setNickname(value.slice(0, MAX_LENGTH));
    if (localError) {
      setLocalError(null);
    }
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const cleaned = nickname.trim();
    if (cleaned.length < MIN_LENGTH) {
      setLocalError(`Your nickname must be ${MIN_LENGTH} to ${MAX_LENGTH} characters.`);
      return;
    }
    setLocalError(null);
    await onSubmit(cleaned);
  };

  return (
    <Card
      elevation={0}
      sx={{
        borderRadius: 3,
        border: '1px solid #E2E8F0',
        boxShadow: '0 20px 25px -5px rgba(9, 19, 31, 0.08), 0 8px 10px -6px rgba(9, 19, 31, 0.04)',
      }}
    >
      <CardContent sx={{ p: { xs: 3, sm: 4.5 } }}>
        <Stack spacing={1} sx={{ textAlign: 'center', mb: 3, alignItems: 'center' }}>
          <Box
            sx={{
              width: 56,
              height: 56,
              borderRadius: '50%',
              bgcolor: '#EEF7FC',
              color: '#00629B',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <PersonIcon sx={{ fontSize: 30 }} />
          </Box>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131F' }}>
            Choose your nickname
          </Typography>
          <Typography variant="body2" sx={{ color: '#486581' }}>
            Joining game{' '}
            <Box
              component="span"
              sx={{ fontWeight: 800, color: '#00629B', letterSpacing: '0.06em' }}
            >
              {pin}
            </Box>
          </Typography>
        </Stack>

        <Box component="form" onSubmit={handleSubmit} noValidate>
          <Stack spacing={2.5}>
            <Box>
              <Stack
                direction="row"
                sx={{ justifyContent: 'space-between', alignItems: 'baseline', mb: 1 }}
              >
                <Typography
                  component="label"
                  htmlFor="nickname-input"
                  variant="subtitle2"
                  sx={{ fontWeight: 600, color: '#09131F' }}
                >
                  Nickname
                </Typography>
                <Typography
                  variant="caption"
                  sx={{
                    fontWeight: 700,
                    color: nickname.length >= MAX_LENGTH ? '#EF4444' : '#64748B',
                  }}
                >
                  {nickname.length} / {MAX_LENGTH}
                </Typography>
              </Stack>
              <TextField
                id="nickname-input"
                fullWidth
                placeholder="Laeth Nueirat"
                value={nickname}
                onChange={(event) => handleChange(event.target.value)}
                required
                autoFocus
                disabled={isLoading}
                error={Boolean(activeError)}
                aria-describedby={activeError ? errorId : undefined}
                autoComplete="nickname"
                slotProps={{
                  input: {
                    startAdornment: (
                      <InputAdornment position="start">
                        <PersonIcon sx={{ color: '#00629B' }} />
                      </InputAdornment>
                    ),
                    sx: {
                      fontWeight: 600,
                      minHeight: 52,
                      fontSize: '1rem',
                    },
                  },
                }}
                helperText={
                  !activeError ? `Between ${MIN_LENGTH} and ${MAX_LENGTH} characters` : undefined
                }
              />
              <InlineFieldError id={errorId} error={activeError} />
            </Box>

            <Button
              type="submit"
              fullWidth
              variant="contained"
              color="primary"
              size="large"
              disabled={isLoading || trimmedLength < MIN_LENGTH}
              startIcon={
                isLoading ? <CircularProgress size={20} color="inherit" /> : <PlayArrowIcon />
              }
              sx={{
                minHeight: 52,
                fontSize: '1.05rem',
                fontWeight: 700,
                borderRadius: 2,
                boxShadow: '0 4px 12px rgba(0, 98, 155, 0.25)',
              }}
            >
              {isLoading ? 'Joining...' : 'Join Game'}
            </Button>

            {onBack && (
              <Button
                onClick={onBack}
                variant="text"
                color="inherit"
                disabled={isLoading}
                startIcon={<ArrowBackIcon />}
                sx={{ minHeight: 44, color: '#486581', fontWeight: 600 }}
              >
                Change PIN
              </Button>
            )}
          </Stack>
        </Box>
      </CardContent>
    </Card>
  );
}

export default NicknameEntryForm;
