import HomeIcon from '@mui/icons-material/Home';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { Box, Button, Container, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

import { MetadataManager } from '../../components/MetadataManager/index.ts';

/**
 * 404 Not Found page with noindex metadata and clear return paths
 * for both players and hosts.
 */
export function NotFoundPage() {
  return (
    <>
      <MetadataManager
        title="Page Not Found (404)"
        description="The requested page could not be found."
        noindex
      />

      <Box
        sx={{
          flexGrow: 1,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          py: 8,
          bgcolor: '#f4f8fc',
        }}
      >
        <Container maxWidth="sm">
          <Stack spacing={3} sx={{ textAlign: 'center', alignItems: 'center' }}>
            <Typography
              variant="h1"
              component="div"
              sx={{
                fontSize: { xs: '4.5rem', sm: '6rem' },
                fontWeight: 900,
                color: '#00629b',
                lineHeight: 1,
                letterSpacing: '-0.04em',
              }}
            >
              404
            </Typography>

            <Typography variant="h4" component="h1" sx={{ fontWeight: 800, color: '#09131f' }}>
              Page Not Found
            </Typography>

            <Typography variant="body1" sx={{ color: '#334e68', maxWidth: 420, lineHeight: 1.6 }}>
              The quiz session or page you are looking for does not exist, has expired, or may have
              been moved.
            </Typography>

            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              spacing={2}
              sx={{ pt: 1, width: { xs: '100%', sm: 'auto' } }}
            >
              <Button
                component={RouterLink}
                to="/"
                variant="contained"
                color="primary"
                size="large"
                startIcon={<HomeIcon />}
                sx={{ minHeight: 48, px: 3, fontWeight: 700 }}
              >
                Join a Game (Home)
              </Button>

              <Button
                component={RouterLink}
                to="/login"
                variant="outlined"
                color="primary"
                size="large"
                startIcon={<LockOutlinedIcon />}
                sx={{ minHeight: 48, px: 3, fontWeight: 600 }}
              >
                Host Portal
              </Button>
            </Stack>
          </Stack>
        </Container>
      </Box>
    </>
  );
}

export default NotFoundPage;
