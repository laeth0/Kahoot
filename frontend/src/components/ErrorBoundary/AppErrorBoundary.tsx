import RefreshIcon from '@mui/icons-material/Refresh';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import { Box, Button, Container, Paper, Stack, Typography } from '@mui/material';
import { Component, type ErrorInfo, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

/**
 * Robust React Error Boundary isolating runtime render crashes
 * with accessible announcement and recovery actions.
 */
export class AppErrorBoundary extends Component<Props, State> {
  public override state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public override componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('AppErrorBoundary caught an unhandled render error:', error, errorInfo);
  }

  private handleReset = () => {
    this.setState({ hasError: false, error: null });
    window.location.reload();
  };

  public override render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <Container maxWidth="sm" sx={{ py: 8 }}>
          <Paper
            elevation={0}
            role="alert"
            sx={{
              p: 4,
              borderRadius: 3,
              border: '1px solid #fee2e2',
              bgcolor: '#fffbfb',
              textAlign: 'center',
            }}
          >
            <Stack spacing={2.5} sx={{ alignItems: 'center' }}>
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
                <WarningAmberIcon sx={{ fontSize: 32 }} />
              </Box>

              <Typography variant="h5" component="h2" sx={{ fontWeight: 700, color: '#09131f' }}>
                Application Render Error
              </Typography>

              <Typography variant="body2" sx={{ color: '#4b5563', lineHeight: 1.6 }}>
                An unexpected interface error occurred. Please refresh or return to the main portal.
              </Typography>

              <Button
                variant="contained"
                color="primary"
                onClick={this.handleReset}
                startIcon={<RefreshIcon />}
                sx={{ minHeight: 44, px: 3, fontWeight: 600 }}
              >
                Reload Page
              </Button>
            </Stack>
          </Paper>
        </Container>
      );
    }

    return this.props.children;
  }
}

export default AppErrorBoundary;
