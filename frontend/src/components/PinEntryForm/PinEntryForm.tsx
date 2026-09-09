import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import TagIcon from '@mui/icons-material/Tag';
import {
  Box,
  Button,
  CircularProgress,
  InputAdornment,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { type FormEvent, useId, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { VALIDATION } from '../../constants/validation.ts';
import { InlineFieldError } from '../Feedback/InlineFieldError.tsx';

export interface PinEntryFormProps {
  initialPin?: string;
  onSubmit?: (pin: string) => void | Promise<void>;
  autoFocus?: boolean;
  disabled?: boolean;
  buttonText?: string;
  size?: 'medium' | 'large';
  helperText?: string;
}

export function PinEntryForm({
  initialPin = '',
  onSubmit,
  autoFocus = true,
  disabled = false,
  buttonText = 'Enter Game',
  size = 'large',
  helperText = 'Ask the host for the 6-digit game PIN',
}: PinEntryFormProps) {
  const navigate = useNavigate();
  const errorId = useId();

  const [pin, setPin] = useState(initialPin);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handlePinChange = (value: string) => {
    const cleaned = value.replace(/[^0-9\s]/g, '').slice(0, 8);
    setPin(cleaned);
    if (errorMessage) {
      setErrorMessage(null);
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const rawPin = pin.replace(/\s+/g, '');

    if (!rawPin || rawPin.length < VALIDATION.PIN.MIN_LENGTH) {
      setErrorMessage(
        `Please enter a valid Game PIN (at least ${VALIDATION.PIN.MIN_LENGTH} digits)`,
      );
      return;
    }

    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      if (onSubmit) {
        await onSubmit(rawPin);
      } else {
        navigate(`/join?pin=${encodeURIComponent(rawPin)}`);
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Could not proceed with this PIN';
      setErrorMessage(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const isLarge = size === 'large';

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate sx={{ width: '100%' }}>
      <Stack spacing={2.5}>
        <Box>
          <Typography
            component="label"
            htmlFor="game-pin-field"
            variant="subtitle2"
            sx={{ fontWeight: 600, color: '#09131f', mb: 1, display: 'block' }}
          >
            Game PIN
          </Typography>
          <TextField
            id="game-pin-field"
            fullWidth
            placeholder="e.g. 739 204"
            value={pin}
            onChange={(e) => handlePinChange(e.target.value)}
            required
            autoFocus={autoFocus}
            disabled={disabled || isSubmitting}
            error={Boolean(errorMessage)}
            aria-describedby={errorMessage ? errorId : undefined}
            slotProps={{
              input: {
                inputMode: 'numeric',
                startAdornment: (
                  <InputAdornment position="start">
                    <TagIcon sx={{ color: '#00629b' }} />
                  </InputAdornment>
                ),
                sx: {
                  fontSize: isLarge ? '1.35rem' : '1.1rem',
                  fontWeight: 700,
                  letterSpacing: '0.08em',
                  minHeight: isLarge ? 54 : 46,
                  bgcolor: '#ffffff',
                },
              },
            }}
            helperText={!errorMessage ? helperText : undefined}
          />
          <InlineFieldError id={errorId} error={errorMessage} />
        </Box>

        <Button
          type="submit"
          fullWidth
          variant="contained"
          color="primary"
          size={isLarge ? 'large' : 'medium'}
          disabled={disabled || isSubmitting}
          startIcon={
            isSubmitting ? <CircularProgress size={20} color="inherit" /> : <PlayArrowIcon />
          }
          sx={{
            minHeight: isLarge ? 52 : 46,
            fontSize: isLarge ? '1.05rem' : '0.95rem',
            fontWeight: 700,
            borderRadius: 2,
            boxShadow: '0 4px 12px rgba(0, 98, 155, 0.25)',
            transition: 'transform 0.15s ease, box-shadow 0.15s ease',
            '&:hover': {
              transform: 'translateY(-1px)',
              boxShadow: '0 6px 16px rgba(0, 98, 155, 0.35)',
            },
          }}
        >
          {isSubmitting ? 'Verifying PIN...' : buttonText}
        </Button>
      </Stack>
    </Box>
  );
}

export default PinEntryForm;
