import { Box, CircularProgress, Skeleton, Stack, Typography } from '@mui/material';

export interface LoadingStateProps {
  variant?: 'spinner' | 'page' | 'card' | 'rows';
  message?: string;
  rowCount?: number;
  minHeight?: number | string;
}

export function LoadingState({
  variant = 'spinner',
  message = 'Loading content, please wait...',
  rowCount = 3,
  minHeight,
}: LoadingStateProps) {
  if (variant === 'page') {
    return (
      <Box
        role="status"
        aria-live="polite"
        sx={{
          minHeight: minHeight || '50vh',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          py: 6,
        }}
      >
        <CircularProgress size={48} color="primary" sx={{ mb: 2 }} />
        <Typography variant="body1" sx={{ color: 'text.secondary', fontWeight: 500 }}>
          {message}
        </Typography>
      </Box>
    );
  }

  if (variant === 'card') {
    return (
      <Box role="status" aria-live="polite" sx={{ width: '100%', p: 3, minHeight: minHeight }}>
        <Skeleton variant="rectangular" height={160} sx={{ borderRadius: 2, mb: 2 }} />
        <Skeleton variant="text" width="60%" height={32} sx={{ mb: 1 }} />
        <Skeleton variant="text" width="90%" height={20} />
      </Box>
    );
  }

  if (variant === 'rows') {
    return (
      <Stack
        role="status"
        aria-live="polite"
        spacing={2}
        sx={{ width: '100%', py: 2, minHeight: minHeight }}
      >
        {Array.from({ length: rowCount }).map((_, idx) => (
          <Skeleton key={idx} variant="rounded" height={56} sx={{ borderRadius: 1.5 }} />
        ))}
      </Stack>
    );
  }

  return (
    <Box
      role="status"
      aria-live="polite"
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 1.5,
        py: 1,
        minHeight: minHeight,
      }}
    >
      <CircularProgress size={24} color="primary" />
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {message}
      </Typography>
    </Box>
  );
}

export default LoadingState;
