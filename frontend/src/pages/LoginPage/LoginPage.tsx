import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import PersonOutlineIcon from '@mui/icons-material/PersonOutline';
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
import { type FormEvent, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import { useAuth } from '../../hooks/useAuth.ts';

export function LoginPage() {
  const { login, isLoading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Read location state for return path if redirected by ProtectedRoute
  const from =
    (location.state as { from?: { pathname: string } })?.from?.pathname || '/host/dashboard';

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    if (!username.trim()) {
      setFormError('Please enter your host username');
      return;
    }

    if (!password) {
      setFormError('Please enter your password');
      return;
    }

    setFormError(null);

    try {
      await login(username.trim(), password);
      navigate(from, { replace: true });
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Invalid host credentials';
      setFormError(msg);
    }
  };

  return (
    <Card
      elevation={0}
      sx={{
        borderRadius: 3,
        border: '1px solid #e2e8f0',
        boxShadow: '0 20px 25px -5px rgba(9, 19, 31, 0.08), 0 8px 10px -6px rgba(9, 19, 31, 0.04)',
      }}
    >
      <CardContent sx={{ p: { xs: 3, sm: 4.5 } }}>
        {/* Header */}
        <Stack spacing={1.5} sx={{ textAlign: 'center', mb: 3.5, alignItems: 'center' }}>
          <Box
            sx={{
              width: 52,
              height: 52,
              borderRadius: '50%',
              bgcolor: '#eef7fc',
              color: '#00629b',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              mb: 1,
            }}
          >
            <LockOutlinedIcon sx={{ fontSize: 28 }} />
          </Box>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 700, color: '#09131f' }}>
            Host Portal Login
          </Typography>
          <Typography variant="body2" sx={{ color: '#334e68' }}>
            Log in to your host account to author quizzes and run live competitive sessions.
          </Typography>
        </Stack>

        {/* Notice for players */}
        <Alert severity="info" variant="standard" sx={{ mb: 3, fontSize: '0.85rem' }}>
          <strong>Looking to play?</strong> Players do not need an account — join with your game PIN
          directly on the home page.
        </Alert>

        {/* Form Error */}
        {formError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {formError}
          </Alert>
        )}

        {/* Login Form */}
        <Box component="form" onSubmit={handleSubmit} noValidate>
          <Stack spacing={2.5}>
            {/* Username Input */}
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
                placeholder="e.g. admin or host_jane"
                autoComplete="username"
                value={username}
                onChange={(e) => {
                  setUsername(e.target.value);
                  if (formError) setFormError(null);
                }}
                required
                autoFocus
                disabled={isLoading}
                slotProps={{
                  input: {
                    startAdornment: (
                      <InputAdornment position="start">
                        <PersonOutlineIcon sx={{ color: '#00629b' }} />
                      </InputAdornment>
                    ),
                    sx: { minHeight: 48 },
                  },
                }}
              />
            </Box>

            {/* Password Input */}
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
                  if (formError) setFormError(null);
                }}
                required
                disabled={isLoading}
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
                    sx: { minHeight: 48 },
                  },
                }}
              />
            </Box>

            {/* Submit Action */}
            <Button
              type="submit"
              fullWidth
              variant="contained"
              color="primary"
              size="large"
              disabled={isLoading}
              startIcon={isLoading ? <CircularProgress size={20} color="inherit" /> : null}
              sx={{
                minHeight: 48,
                fontSize: '1rem',
                fontWeight: 700,
                mt: 1,
                boxShadow: '0 4px 10px rgba(0, 98, 155, 0.25)',
              }}
            >
              {isLoading ? 'Signing in...' : 'Sign In as Host'}
            </Button>
          </Stack>
        </Box>
      </CardContent>
    </Card>
  );
}

export default LoginPage;
