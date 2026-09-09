import {
  Alert,
  AppBar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Container,
  CssBaseline,
  Divider,
  Paper,
  Stack,
  TextField,
  Toolbar,
  Typography,
} from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';

import logo from './assets/logo.jpeg';
import { theme } from './theme/index.ts';

function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
        {/* Navigation Bar */}
        <AppBar position="static">
          <Container maxWidth="lg">
            <Toolbar disableGutters sx={{ justifyContent: 'space-between', py: 1 }}>
              <Stack direction="row" spacing={2} alignItems="center">
                <Box
                  component="img"
                  src={logo}
                  alt="IEEEXtreme Palestine Section Logo"
                  sx={{
                    width: 44,
                    height: 44,
                    borderRadius: '50%',
                    objectFit: 'cover',
                    border: '2px solid',
                    borderColor: 'primary.main',
                  }}
                />
                <Box>
                  <Typography variant="h6" component="h1" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
                    Kahoot Platform
                  </Typography>
                  <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 500 }}>
                    IEEEXtreme Palestine Section
                  </Typography>
                </Box>
              </Stack>
              <Stack direction="row" spacing={1} alignItems="center">
                <Chip
                  label="Light Theme Only"
                  size="small"
                  color="primary"
                  variant="outlined"
                  sx={{ fontWeight: 600 }}
                />
                <Button variant="contained" color="primary">
                  Join Game
                </Button>
              </Stack>
            </Toolbar>
          </Container>
        </AppBar>

        {/* Hero Section */}
        <Box
          component="main"
          sx={{
            flexGrow: 1,
            py: 6,
            background: 'linear-gradient(180deg, #eef7fc 0%, #f4f8fc 100%)',
          }}
        >
          <Container maxWidth="lg">
            {/* Header Banner */}
            <Paper
              elevation={0}
              sx={{
                p: { xs: 3, md: 5 },
                mb: 4,
                borderRadius: 3,
                border: '1px solid',
                borderColor: 'divider',
                background:
                  'radial-gradient(ellipse at 80% 20%, rgba(2, 132, 199, 0.08) 0%, #ffffff 70%)',
              }}
            >
              <Stack
                direction={{ xs: 'column', md: 'row' }}
                spacing={4}
                alignItems="center"
                justifyContent="space-between"
              >
                <Box sx={{ maxWidth: 640 }}>
                  <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
                    <Chip label="IEEE Ocean Blue: #00629B" size="small" color="primary" />
                    <Chip label="Electric Cyan: #0284C7" size="small" color="secondary" />
                  </Stack>
                  <Typography variant="h2" component="h2" gutterBottom>
                    Engage, Compete & Learn
                  </Typography>
                  <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
                    Welcome to the official Kahoot platform themed specifically with the IEEEXtreme
                    Palestine Section brand colors. Designed strictly for modern light environments
                    with maximum visual clarity and accessibility.
                  </Typography>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
                    <Button variant="contained" color="primary" size="large">
                      Host a Session
                    </Button>
                    <Button variant="outlined" color="primary" size="large">
                      Explore Quizzes
                    </Button>
                  </Stack>
                </Box>

                <Box
                  component="img"
                  src={logo}
                  alt="Brand Emblem"
                  sx={{
                    width: { xs: 160, md: 220 },
                    height: { xs: 160, md: 220 },
                    borderRadius: 4,
                    boxShadow:
                      '0 20px 25px -5px rgba(9, 19, 31, 0.12), 0 8px 10px -6px rgba(9, 19, 31, 0.06)',
                    border: '1px solid',
                    borderColor: 'divider',
                  }}
                />
              </Stack>
            </Paper>

            {/* Component Showcase Cards */}
            <Typography variant="h4" component="h3" sx={{ mb: 3 }}>
              Theme Showcase & Color System
            </Typography>

            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' },
                gap: 3,
                mb: 4,
              }}
            >
              {/* Card 1: Brand Swatches */}
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Brand Color Palette
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    Extracted from the logo gradients and radar rings:
                  </Typography>
                  <Stack spacing={1.5}>
                    <Box
                      sx={{
                        p: 1.5,
                        borderRadius: 1,
                        bgcolor: 'primary.main',
                        color: 'primary.contrastText',
                        display: 'flex',
                        justifyContent: 'space-between',
                      }}
                    >
                      <Typography variant="body2" fontWeight={600}>
                        Primary Brand
                      </Typography>
                      <Typography variant="caption">#00629B</Typography>
                    </Box>
                    <Box
                      sx={{
                        p: 1.5,
                        borderRadius: 1,
                        bgcolor: 'secondary.main',
                        color: 'secondary.contrastText',
                        display: 'flex',
                        justifyContent: 'space-between',
                      }}
                    >
                      <Typography variant="body2" fontWeight={600}>
                        Radar Cyan Accent
                      </Typography>
                      <Typography variant="caption">#0284C7</Typography>
                    </Box>
                    <Box
                      sx={{
                        p: 1.5,
                        borderRadius: 1,
                        bgcolor: '#09131f',
                        color: '#ffffff',
                        display: 'flex',
                        justifyContent: 'space-between',
                      }}
                    >
                      <Typography variant="body2" fontWeight={600}>
                        Deep Tech Navy
                      </Typography>
                      <Typography variant="caption">#09131F</Typography>
                    </Box>
                    <Box
                      sx={{
                        p: 1.5,
                        borderRadius: 1,
                        bgcolor: 'background.default',
                        border: '1px solid',
                        borderColor: 'divider',
                        display: 'flex',
                        justifyContent: 'space-between',
                      }}
                    >
                      <Typography variant="body2" fontWeight={600} color="text.primary">
                        Light Surface Canvas
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        #F4F8FC
                      </Typography>
                    </Box>
                  </Stack>
                </CardContent>
              </Card>

              {/* Card 2: Interactive Controls */}
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Form Inputs & Buttons
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    Styled with 8px radius and custom brand focus states:
                  </Typography>
                  <Stack spacing={2}>
                    <TextField
                      fullWidth
                      label="Game PIN"
                      placeholder="Enter 6-digit PIN"
                      size="small"
                      defaultValue="739 204"
                    />
                    <TextField
                      fullWidth
                      label="Player Nickname"
                      placeholder="e.g. ExtremeCoder"
                      size="small"
                    />
                    <Stack direction="row" spacing={1}>
                      <Button fullWidth variant="contained" color="primary">
                        Enter
                      </Button>
                      <Button fullWidth variant="outlined" color="primary">
                        Cancel
                      </Button>
                    </Stack>
                  </Stack>
                </CardContent>
              </Card>

              {/* Card 3: Feedback & Alerts */}
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Semantic Alerts
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    Harmonized status alerts for competitive gaming:
                  </Typography>
                  <Stack spacing={1.5}>
                    <Alert severity="info" variant="standard">
                      Session starts in 2 minutes.
                    </Alert>
                    <Alert severity="success" variant="standard">
                      Answer submitted: +950 pts!
                    </Alert>
                    <Alert severity="warning" variant="standard">
                      10 seconds remaining!
                    </Alert>
                  </Stack>
                </CardContent>
              </Card>
            </Box>
          </Container>
        </Box>

        {/* Footer */}
        <Box
          component="footer"
          sx={{
            py: 3,
            px: 2,
            backgroundColor: '#ffffff',
            borderTop: '1px solid',
            borderColor: 'divider',
          }}
        >
          <Container maxWidth="lg">
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              justifyContent="space-between"
              alignItems="center"
              spacing={2}
            >
              <Typography variant="body2" color="text.secondary">
                © {new Date().getFullYear()} IEEEXtreme Palestine Section. Light Theme Active.
              </Typography>
              <Stack
                direction="row"
                spacing={2}
                divider={<Divider orientation="vertical" flexItem />}
              >
                <Typography variant="caption" color="text.secondary">
                  Light Theme Enforced
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  WCAG 2.2 AA Compliant
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Material UI v9
                </Typography>
              </Stack>
            </Stack>
          </Container>
        </Box>
      </Box>
    </ThemeProvider>
  );
}

export default App;
