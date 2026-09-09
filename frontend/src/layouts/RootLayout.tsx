import LogoutIcon from '@mui/icons-material/Logout';
import PersonIcon from '@mui/icons-material/Person';
import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import {
  AppBar,
  Box,
  Button,
  Chip,
  Container,
  Divider,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material';
import { Link as RouterLink, Outlet, useNavigate } from 'react-router-dom';

import logo from '../assets/logo.jpeg';
import { useAuth } from '../hooks/useAuth.ts';

export function RootLayout() {
  const { host, isAuthenticated, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/');
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      {/* Accessible Skip Link */}
      <Box
        component="a"
        href="#main-content"
        sx={{
          position: 'absolute',
          top: -999,
          left: 16,
          zIndex: 9999,
          p: 1.5,
          bgcolor: 'primary.main',
          color: '#ffffff',
          borderRadius: 1,
          fontWeight: 600,
          textDecoration: 'none',
          '&:focus': {
            top: 16,
          },
        }}
      >
        Skip to main content
      </Box>

      {/* Floating Modern Header */}
      <AppBar position="sticky" sx={{ bgcolor: '#ffffff', borderBottom: '1px solid #e2e8f0' }}>
        <Container maxWidth="lg">
          <Toolbar disableGutters sx={{ justifyContent: 'space-between', py: 1.25 }}>
            {/* Brand Logo & Title */}
            <Stack
              component={RouterLink}
              to="/"
              direction="row"
              spacing={2}
              sx={{
                alignItems: 'center',
                textDecoration: 'none',
                color: 'inherit',
                cursor: 'pointer',
              }}
            >
              <Box
                component="img"
                src={logo}
                alt="IEEEXtreme Palestine Section Logo"
                sx={{
                  width: 44,
                  height: 44,
                  borderRadius: '50%',
                  objectFit: 'cover',
                  border: '2px solid #00629b',
                  boxShadow: '0 2px 4px rgba(0, 98, 155, 0.15)',
                }}
              />
              <Box>
                <Typography
                  variant="h6"
                  component="h1"
                  sx={{
                    fontWeight: 700,
                    lineHeight: 1.2,
                    color: '#09131f',
                    letterSpacing: '-0.02em',
                  }}
                >
                  Kahoot Platform
                </Typography>
                <Typography
                  variant="caption"
                  sx={{ color: '#334e68', fontWeight: 500, display: 'block' }}
                >
                  IEEEXtreme Palestine Section
                </Typography>
              </Box>
            </Stack>

            {/* Navigation Actions */}
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
              <Button
                component={RouterLink}
                to="/"
                color="inherit"
                startIcon={<SportsEsportsIcon />}
                sx={{
                  color: '#334e68',
                  fontWeight: 600,
                  minHeight: 44,
                  px: 2,
                  '&:hover': { color: '#00629b', bgcolor: 'rgba(0, 98, 155, 0.04)' },
                }}
              >
                Join Game
              </Button>

              {isAuthenticated && host ? (
                <>
                  <Chip
                    icon={<PersonIcon />}
                    label={`Host: ${host.username}`}
                    variant="outlined"
                    color="primary"
                    sx={{ fontWeight: 600, height: 36 }}
                  />
                  <Button
                    component={RouterLink}
                    to="/host/dashboard"
                    variant="contained"
                    color="primary"
                    sx={{ minHeight: 44, px: 2 }}
                  >
                    Dashboard
                  </Button>
                  <Button
                    variant="outlined"
                    color="inherit"
                    onClick={handleLogout}
                    startIcon={<LogoutIcon />}
                    aria-label="Log out of host account"
                    sx={{
                      minHeight: 44,
                      borderColor: '#cbd5e1',
                      color: '#334e68',
                      '&:hover': { borderColor: '#dc2626', color: '#dc2626' },
                    }}
                  >
                    Logout
                  </Button>
                </>
              ) : (
                <Button
                  component={RouterLink}
                  to="/login"
                  variant="outlined"
                  color="primary"
                  sx={{ minHeight: 44, px: 2.5, fontWeight: 600 }}
                >
                  Host Login
                </Button>
              )}
            </Stack>
          </Toolbar>
        </Container>
      </AppBar>

      {/* Main Content Area */}
      <Box
        id="main-content"
        component="main"
        tabIndex={-1}
        sx={{
          flexGrow: 1,
          display: 'flex',
          flexDirection: 'column',
          outline: 'none',
        }}
      >
        <Outlet />
      </Box>

      {/* Footer */}
      <Box
        component="footer"
        sx={{
          py: 3,
          px: 2,
          backgroundColor: '#ffffff',
          borderTop: '1px solid #e2e8f0',
        }}
      >
        <Container maxWidth="lg">
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            sx={{ justifyContent: 'space-between', alignItems: 'center' }}
            spacing={2}
          >
            <Typography variant="body2" color="text.secondary">
              © {new Date().getFullYear()} IEEEXtreme Palestine Section.
            </Typography>
            <Stack
              direction="row"
              spacing={2}
              divider={<Divider orientation="vertical" flexItem />}
            >
              <Typography variant="caption" color="text.secondary">
                Light Theme Active
              </Typography>
              <Typography variant="caption" color="text.secondary">
                WCAG 2.2 AA Compliant
              </Typography>
              <Typography variant="caption" color="text.secondary">
                React 19 & MUI v9
              </Typography>
            </Stack>
          </Stack>
        </Container>
      </Box>
    </Box>
  );
}

export default RootLayout;
