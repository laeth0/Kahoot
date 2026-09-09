import ErrorIcon from '@mui/icons-material/Error';
import { Box, Typography } from '@mui/material';

export interface InlineFieldErrorProps {
  id?: string;
  error?: string | null;
}

export function InlineFieldError({ id, error }: InlineFieldErrorProps) {
  if (!error) return null;

  return (
    <Box
      id={id}
      role="alert"
      aria-live="polite"
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 0.75,
        mt: 0.75,
        color: 'error.main',
      }}
    >
      <ErrorIcon sx={{ fontSize: 16 }} />
      <Typography variant="caption" sx={{ fontWeight: 600, color: 'inherit' }}>
        {error}
      </Typography>
    </Box>
  );
}

export default InlineFieldError;
