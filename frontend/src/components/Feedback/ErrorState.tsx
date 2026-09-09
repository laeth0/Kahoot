import ErrorOutlinedIcon from '@mui/icons-material/ErrorOutlined';
import RefreshIcon from '@mui/icons-material/Refresh';
import { Alert, Box, Button, Paper, Stack, Typography } from '@mui/material';

export interface ErrorStateProps {
  title?: string;
  message: string;
  onRetry?: () => void;
  retryText?: string;
  status?: 400 | 401 | 403 | 404 | 429 | 500;
  variant?: 'inline' | 'panel';
}

export function ErrorState({
  title = 'Something went wrong',
  message,
  onRetry,
  retryText = 'Try Again',
  variant = 'panel',
}: ErrorStateProps) {
  if (variant === 'inline') {
    return (
      <Alert
        severity="error"
        role="alert"
        action={
          onRetry ? (
            <Button color="inherit" size="small" onClick={onRetry} startIcon={<RefreshIcon />}>
              {retryText}
            </Button>
          ) : undefined
        }
        sx={{ my: 1 }}
      >
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {message}
        </Typography>
      </Alert>
    );
  }

  return (
    <Paper
      elevation={0}
      role="alert"
      sx={{
        p: { xs: 4, sm: 6 },
        textAlign: 'center',
        borderRadius: 3,
        border: '1px solid #fee2e2',
        bgcolor: '#fffbfb',
      }}
    >
      <Stack spacing={2} sx={{ alignItems: 'center', maxWidth: 460, mx: 'auto' }}>
        <Box
          sx={{
            width: 56,
            height: 56,
            borderRadius: '50%',
            bgcolor: '#fee2e2',
            color: '#dc2626',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <ErrorOutlinedIcon sx={{ fontSize: 32 }} />
        </Box>

        <Typography variant="h6" component="h3" sx={{ fontWeight: 700, color: '#09131f' }}>
          {title}
        </Typography>

        <Typography variant="body2" sx={{ color: '#4b5563', lineHeight: 1.6 }}>
          {message}
        </Typography>

        {onRetry && (
          <Button
            variant="outlined"
            color="primary"
            onClick={onRetry}
            startIcon={<RefreshIcon />}
            sx={{ minHeight: 44, px: 3, mt: 1, fontWeight: 600 }}
          >
            {retryText}
          </Button>
        )}
      </Stack>
    </Paper>
  );
}

export default ErrorState;
