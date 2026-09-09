import HomeIcon from '@mui/icons-material/Home';
import { Box, Button, Container, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

export function NotFoundPage() {
  return (
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
            sx={{
              fontSize: '5rem',
              fontWeight: 900,
              color: '#00629b',
              lineHeight: 1,
            }}
          >
            404
          </Typography>
          <Typography variant="h4" component="h2" sx={{ fontWeight: 700, color: '#09131f' }}>
            Page Not Found
          </Typography>
          <Typography variant="body1" sx={{ color: '#334e68', maxWidth: 400 }}>
            The requested quiz or page does not exist or may have been moved.
          </Typography>
          <Button
            component={RouterLink}
            to="/"
            variant="contained"
            color="primary"
            size="large"
            startIcon={<HomeIcon />}
            sx={{ minHeight: 48, px: 3, fontWeight: 700 }}
          >
            Return to Home
          </Button>
        </Stack>
      </Container>
    </Box>
  );
}

export default NotFoundPage;
