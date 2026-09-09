import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { Box, Button, Container, Stack, Typography } from '@mui/material';
import { Link as RouterLink, Outlet } from 'react-router-dom';

import logo from '../assets/logo.jpeg';

export function AuthLayout() {
  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: '#f4f8fc',
        py: { xs: 3, sm: 6 },
      }}
    >
      <Container maxWidth="sm">
        {/* Top Back Navigation */}
        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 3 }}
        >
          <Button
            component={RouterLink}
            to="/"
            startIcon={<ArrowBackIcon />}
            sx={{
              color: '#334e68',
              fontWeight: 600,
              minHeight: 44,
              '&:hover': { color: '#00629b' },
            }}
          >
            Back to Game Join
          </Button>

          <Stack
            component={RouterLink}
            to="/"
            direction="row"
            spacing={1.5}
            sx={{
              alignItems: 'center',
              textDecoration: 'none',
              color: 'inherit',
            }}
          >
            <Box
              component="img"
              src={logo}
              alt="IEEEXtreme Palestine Section Logo"
              sx={{
                width: 32,
                height: 32,
                borderRadius: '50%',
                border: '1.5px solid #00629b',
              }}
            />
            <Typography variant="subtitle2" sx={{ fontWeight: 700, color: '#09131f' }}>
              Kahoot
            </Typography>
          </Stack>
        </Stack>

        {/* Auth Content Outlet */}
        <Box component="main">
          <Outlet />
        </Box>
      </Container>
    </Box>
  );
}

export default AuthLayout;
