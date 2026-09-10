import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import PersonOutlinedIcon from '@mui/icons-material/PersonOutlined';
import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { type FormEvent, useEffect, useId, useState } from 'react';
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom';

import { InlineFieldError } from '../../components/Feedback/InlineFieldError.tsx';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { getFriendlyErrorMessage } from '../../constants/errorCodes.ts';
import { VALIDATION } from '../../constants/validation.ts';
import { useAuth } from '../../hooks/useAuth.ts';

export function LoginPage() {
  const { login, isLoading, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const usernameErrorId = useId();
  const passwordErrorId = useId();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [usernameError, setUsernameError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(() => {
    if (
      typeof window !== 'undefined' &&
      sessionStorage.getItem('kahoot_session_expired') === 'true'
    ) {
      sessionStorage.removeItem('kahoot_session_expired');
      return 'Your session has expired. Please sign in again.';
    }
    return null;
  });

  const queryParams = new URLSearchParams(location.search);
  const returnUrl = queryParams.get('returnUrl');
  const from =
    returnUrl ||
    (location.state as { from?: { pathname: string } })?.from?.pathname ||
    '/host/dashboard';

  useEffect(() => {
    if (isAuthenticated) {
      navigate(from, { replace: true });
    }
  }, [isAuthenticated, navigate, from]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    let hasError = false;

    if (!username.trim()) {
      setUsernameError('Please enter your host username');
      hasError = true;
    } else if (username.trim().length < VALIDATION.HOST.USERNAME_MIN_LENGTH) {
      setUsernameError(
        `Username must be at least ${VALIDATION.HOST.USERNAME_MIN_LENGTH} characters`,
      );
      hasError = true;
    } else {
      setUsernameError(null);
    }

    if (!password) {
      setPasswordError('Please enter your host password');
      hasError = true;
    } else {
      setPasswordError(null);
    }

    if (hasError) return;

    setFormError(null);

    try {
      await login(username.trim(), password);
      navigate(from, { replace: true });
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Invalid host credentials';
      setFormError(getFriendlyErrorMessage(msg));
    }
  };

  return (
    <>
      <MetadataManager
        title="Host Sign In"
        description="Sign in to your host account to author quizzes, manage questions, and launch live competitive gaming sessions."
      />

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
                width: 54,
                height: 54,
                borderRadius: '50%',
                bgcolor: '#eef7fc',
                color: '#00629b',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                mb: 0.5,
              }}
            >
              <LockOutlinedIcon sx={{ fontSize: 28 }} />
            </Box>
            <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131f' }}>
              Host Portal Login
            </Typography>
            <Typography variant="body2" sx={{ color: '#334e68' }}>
              Sign in to author quizzes, manage live sessions, and track real-time scores.
            </Typography>
          </Stack>

          <Alert
            severity="info"
            icon={<SportsEsportsIcon />}
            sx={{
              mb: 3,
              fontSize: '0.9rem',
              bgcolor: '#eef7fc',
              color: '#00629b',
              border: '1px solid #bae6fd',
              '& .MuiAlert-icon': { color: '#00629b' },
            }}
          >
            <Box>
              <strong>Looking to play?</strong> Players do not need an account.{' '}
              <Button
                component={RouterLink}
                to="/"
                size="small"
                sx={{
                  fontWeight: 700,
                  p: 0,
                  minWidth: 'auto',
                  textDecoration: 'underline',
                  verticalAlign: 'baseline',
                }}
              >
                Join with a Game PIN here
              </Button>
            </Box>
          </Alert>

          {formError && (
            <Alert severity="error" role="alert" sx={{ mb: 3 }}>
              {formError}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit} noValidate>
            <Stack spacing={2.5}>
              <Box>
                <Typography
                  component="label"
                  htmlFor="username-field"
                  variant="subtitle2"
                  sx={{ fontWeight: 600, color: '#09131f', mb: 0.75, display: 'block' }}
                >
                  Host Username
                </Typography>
                <TextField
                  id="username-field"
                  fullWidth
                  placeholder="e.g. admin or host_user"
                  autoComplete="username"
                  value={username}
                  onChange={(e) => {
                    setUsername(e.target.value);
                    if (usernameError) setUsernameError(null);
                    if (formError) setFormError(null);
                  }}
                  required
                  autoFocus
                  disabled={isLoading}
                  error={Boolean(usernameError)}
                  aria-describedby={usernameError ? usernameErrorId : undefined}
                  slotProps={{
                    input: {
                      startAdornment: (
                        <InputAdornment position="start">
                          <PersonOutlinedIcon sx={{ color: '#00629b' }} />
                        </InputAdornment>
                      ),
                      sx: { minHeight: 50 },
                    },
                  }}
                />
                <InlineFieldError id={usernameErrorId} error={usernameError} />
              </Box>

              <Box>
                <Typography
                  component="label"
                  htmlFor="password-field"
                  variant="subtitle2"
                  sx={{ fontWeight: 600, color: '#09131f', mb: 0.75, display: 'block' }}
                >
                  Password
                </Typography>
                <TextField
                  id="password-field"
                  fullWidth
                  type={showPassword ? 'text' : 'password'}
                  placeholder="Enter your password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value);
                    if (passwordError) setPasswordError(null);
                    if (formError) setFormError(null);
                  }}
                  required
                  disabled={isLoading}
                  error={Boolean(passwordError)}
                  aria-describedby={passwordError ? passwordErrorId : undefined}
                  slotProps={{
                    input: {
                      startAdornment: (
                        <InputAdornment position="start">
                          <LockOutlinedIcon sx={{ color: '#00629b' }} />
                        </InputAdornment>
                      ),
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            aria-label={showPassword ? 'Hide password' : 'Show password'}
                            onClick={() => setShowPassword((prev) => !prev)}
                            edge="end"
                            size="small"
                          >
                            {showPassword ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </InputAdornment>
                      ),
                      sx: { minHeight: 50 },
                    },
                  }}
                />
                <InlineFieldError id={passwordErrorId} error={passwordError} />
              </Box>

              <Button
                type="submit"
                fullWidth
                variant="contained"
                color="primary"
                size="large"
                disabled={isLoading}
                startIcon={isLoading ? <CircularProgress size={20} color="inherit" /> : null}
                sx={{
                  minHeight: 50,
                  fontSize: '1rem',
                  fontWeight: 700,
                  mt: 1,
                  borderRadius: 2,
                  boxShadow: '0 4px 12px rgba(0, 98, 155, 0.25)',
                }}
              >
                {isLoading ? 'Signing in...' : 'Sign In as Host'}
              </Button>
            </Stack>
          </Box>
        </CardContent>
      </Card>
    </>
  );
}

export default LoginPage;
