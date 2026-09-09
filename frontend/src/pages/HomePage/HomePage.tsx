import BoltIcon from '@mui/icons-material/Bolt';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import SecurityIcon from '@mui/icons-material/Security';
import SpeedIcon from '@mui/icons-material/Speed';
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Container,
  Divider,
  Grid,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

import logo from '../../assets/logo.jpeg';
import { MetadataManager } from '../../components/MetadataManager/index.ts';
import { PinEntryForm } from '../../components/PinEntryForm/index.ts';

export function HomePage() {
  return (
    <>
      <MetadataManager
        title="Live Engineering Quizzes & Competitions"
        description="Join live real-time engineering quizzes hosted by IEEEXtreme Palestine Section. Enter your Game PIN and compete on the live leaderboard without registration."
      />

      <Box
        sx={{
          flexGrow: 1,
          background: 'linear-gradient(180deg, #eef7fc 0%, #f4f8fc 60%, #ffffff 100%)',
          py: { xs: 4, md: 8 },
        }}
      >
        <Container maxWidth="lg">
          <Box component="section" aria-labelledby="hero-title" sx={{ mb: { xs: 6, md: 10 } }}>
            <Stack spacing={3} sx={{ textAlign: 'center', alignItems: 'center', mb: 5 }}>
              <Chip
                icon={
                  <Box
                    sx={{
                      width: 8,
                      height: 8,
                      borderRadius: '50%',
                      bgcolor: '#059669',
                      boxShadow: '0 0 0 3px rgba(5, 150, 105, 0.25)',
                      mr: 0.5,
                    }}
                  />
                }
                label="IEEEXtreme Palestine Section • Live Arena"
                variant="outlined"
                sx={{
                  fontWeight: 600,
                  fontSize: '0.85rem',
                  borderColor: '#bae6fd',
                  bgcolor: '#ffffff',
                  color: '#00629b',
                  py: 1,
                  px: 0.5,
                }}
              />

              <Box
                component="img"
                src={logo}
                alt="IEEEXtreme Palestine Section Emblem"
                sx={{
                  width: { xs: 72, sm: 84 },
                  height: { xs: 72, sm: 84 },
                  borderRadius: '50%',
                  objectFit: 'cover',
                  border: '3px solid #00629b',
                  boxShadow: '0 8px 20px rgba(0, 98, 155, 0.18)',
                }}
              />

              <Typography
                id="hero-title"
                variant="h1"
                component="h1"
                sx={{
                  fontWeight: 800,
                  fontSize: { xs: '2.1rem', sm: '2.8rem', md: '3.4rem' },
                  color: '#09131f',
                  letterSpacing: '-0.03em',
                  lineHeight: 1.15,
                  maxWidth: 780,
                }}
              >
                Real-Time Competitive Quizzing for Engineers
              </Typography>

              <Typography
                variant="body1"
                sx={{
                  color: '#334e68',
                  maxWidth: 580,
                  fontSize: { xs: '1rem', md: '1.15rem' },
                  lineHeight: 1.6,
                }}
              >
                Jump straight into live technical trivia, algorithms, and speed challenges. No
                account or sign-up needed for players.
              </Typography>
            </Stack>

            <Card
              elevation={0}
              sx={{
                maxWidth: 480,
                mx: 'auto',
                borderRadius: 3,
                border: '1px solid #e2e8f0',
                bgcolor: '#ffffff',
                boxShadow:
                  '0 20px 30px -10px rgba(9, 19, 31, 0.08), 0 10px 15px -5px rgba(9, 19, 31, 0.04)',
                overflow: 'visible',
              }}
            >
              <CardContent sx={{ p: { xs: 3, sm: 4.5 } }}>
                <Typography
                  variant="h5"
                  component="h2"
                  sx={{
                    fontWeight: 700,
                    color: '#09131f',
                    mb: 1,
                    textAlign: 'center',
                  }}
                >
                  Join a Live Game
                </Typography>
                <Typography variant="body2" sx={{ color: '#334e68', mb: 3, textAlign: 'center' }}>
                  Enter the 6-digit PIN shown on the host's screen
                </Typography>

                <PinEntryForm size="large" buttonText="Continue to Game" />

                <Divider sx={{ my: 3 }}>
                  <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 500 }}>
                    OR
                  </Typography>
                </Divider>

                <Button
                  component={RouterLink}
                  to="/login"
                  fullWidth
                  variant="outlined"
                  color="primary"
                  size="large"
                  startIcon={<LockOutlinedIcon />}
                  sx={{
                    minHeight: 48,
                    fontWeight: 600,
                    borderColor: '#cbd5e1',
                    color: '#00629b',
                    '&:hover': {
                      borderColor: '#00629b',
                      bgcolor: 'rgba(0, 98, 155, 0.04)',
                    },
                  }}
                >
                  Host Sign In / Dashboard
                </Button>
              </CardContent>
            </Card>
          </Box>

          <Box
            component="section"
            aria-labelledby="how-it-works-title"
            sx={{ mb: { xs: 6, md: 10 } }}
          >
            <Typography
              id="how-it-works-title"
              variant="h4"
              component="h2"
              sx={{
                fontWeight: 700,
                textAlign: 'center',
                color: '#09131f',
                mb: 1,
                letterSpacing: '-0.02em',
              }}
            >
              How to Participate
            </Typography>
            <Typography
              variant="body1"
              sx={{ textAlign: 'center', color: '#334e68', mb: 5, maxWidth: 500, mx: 'auto' }}
            >
              Joining an IEEEXtreme interactive session takes three simple steps.
            </Typography>

            <Grid container spacing={3}>
              <Grid size={{ xs: 12, md: 4 }}>
                <Paper
                  elevation={0}
                  sx={{
                    p: 3.5,
                    height: '100%',
                    borderRadius: 3,
                    border: '1px solid #e2e8f0',
                    bgcolor: '#ffffff',
                    transition: 'transform 0.2s ease, box-shadow 0.2s ease',
                    '&:hover': {
                      transform: 'translateY(-3px)',
                      boxShadow: '0 12px 20px -5px rgba(0, 98, 155, 0.08)',
                    },
                  }}
                >
                  <Stack spacing={2}>
                    <Box
                      sx={{
                        width: 48,
                        height: 48,
                        borderRadius: 2,
                        bgcolor: '#eef7fc',
                        color: '#00629b',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontWeight: 800,
                        fontSize: '1.25rem',
                      }}
                    >
                      1
                    </Box>
                    <Typography
                      variant="h6"
                      component="h3"
                      sx={{ fontWeight: 700, color: '#09131f' }}
                    >
                      Get the PIN
                    </Typography>
                    <Typography variant="body2" sx={{ color: '#334e68', lineHeight: 1.6 }}>
                      Ask your event host or check the projector screen for the unique 6-digit game
                      PIN.
                    </Typography>
                  </Stack>
                </Paper>
              </Grid>

              <Grid size={{ xs: 12, md: 4 }}>
                <Paper
                  elevation={0}
                  sx={{
                    p: 3.5,
                    height: '100%',
                    borderRadius: 3,
                    border: '1px solid #e2e8f0',
                    bgcolor: '#ffffff',
                    transition: 'transform 0.2s ease, box-shadow 0.2s ease',
                    '&:hover': {
                      transform: 'translateY(-3px)',
                      boxShadow: '0 12px 20px -5px rgba(0, 98, 155, 0.08)',
                    },
                  }}
                >
                  <Stack spacing={2}>
                    <Box
                      sx={{
                        width: 48,
                        height: 48,
                        borderRadius: 2,
                        bgcolor: '#eef7fc',
                        color: '#00629b',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontWeight: 800,
                        fontSize: '1.25rem',
                      }}
                    >
                      2
                    </Box>
                    <Typography
                      variant="h6"
                      component="h3"
                      sx={{ fontWeight: 700, color: '#09131f' }}
                    >
                      Pick Your Handle
                    </Typography>
                    <Typography variant="body2" sx={{ color: '#334e68', lineHeight: 1.6 }}>
                      Choose your player nickname. No password or email needed—you are in the lobby
                      immediately.
                    </Typography>
                  </Stack>
                </Paper>
              </Grid>

              <Grid size={{ xs: 12, md: 4 }}>
                <Paper
                  elevation={0}
                  sx={{
                    p: 3.5,
                    height: '100%',
                    borderRadius: 3,
                    border: '1px solid #e2e8f0',
                    bgcolor: '#ffffff',
                    transition: 'transform 0.2s ease, box-shadow 0.2s ease',
                    '&:hover': {
                      transform: 'translateY(-3px)',
                      boxShadow: '0 12px 20px -5px rgba(0, 98, 155, 0.08)',
                    },
                  }}
                >
                  <Stack spacing={2}>
                    <Box
                      sx={{
                        width: 48,
                        height: 48,
                        borderRadius: 2,
                        bgcolor: '#eef7fc',
                        color: '#00629b',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontWeight: 800,
                        fontSize: '1.25rem',
                      }}
                    >
                      3
                    </Box>
                    <Typography
                      variant="h6"
                      component="h3"
                      sx={{ fontWeight: 700, color: '#09131f' }}
                    >
                      Compete Live
                    </Typography>
                    <Typography variant="body2" sx={{ color: '#334e68', lineHeight: 1.6 }}>
                      Answer quickly to earn speed multipliers, see real-time distribution charts,
                      and conquer the podium.
                    </Typography>
                  </Stack>
                </Paper>
              </Grid>
            </Grid>
          </Box>

          <Box component="section" aria-labelledby="features-title" sx={{ mb: 4 }}>
            <Paper
              elevation={0}
              sx={{
                p: { xs: 3, sm: 5 },
                borderRadius: 3,
                border: '1px solid #e2e8f0',
                bgcolor: '#ffffff',
              }}
            >
              <Typography
                id="features-title"
                variant="h5"
                component="h2"
                sx={{
                  fontWeight: 700,
                  color: '#09131f',
                  mb: 3,
                  textAlign: { xs: 'left', sm: 'center' },
                }}
              >
                Engineered for High-Concurrence Competition
              </Typography>

              <Grid container spacing={3}>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start' }}>
                    <BoltIcon sx={{ color: '#00629b', fontSize: 28, mt: 0.25 }} />
                    <Box>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, color: '#09131f' }}>
                        Live Synchronization
                      </Typography>
                      <Typography variant="body2" sx={{ color: '#334e68', mt: 0.5 }}>
                        Sub-second question delivery powered by SignalR WebSockets.
                      </Typography>
                    </Box>
                  </Stack>
                </Grid>

                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start' }}>
                    <SpeedIcon sx={{ color: '#0284c7', fontSize: 28, mt: 0.25 }} />
                    <Box>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, color: '#09131f' }}>
                        Dynamic Scoring
                      </Typography>
                      <Typography variant="body2" sx={{ color: '#334e68', mt: 0.5 }}>
                        Precision point decay based on server-side response times.
                      </Typography>
                    </Box>
                  </Stack>
                </Grid>

                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start' }}>
                    <LeaderboardIcon sx={{ color: '#059669', fontSize: 28, mt: 0.25 }} />
                    <Box>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, color: '#09131f' }}>
                        Live Standings
                      </Typography>
                      <Typography variant="body2" sx={{ color: '#334e68', mt: 0.5 }}>
                        Rank updates, delta indicators, and animated podium reveals.
                      </Typography>
                    </Box>
                  </Stack>
                </Grid>

                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start' }}>
                    <SecurityIcon sx={{ color: '#00629b', fontSize: 28, mt: 0.25 }} />
                    <Box>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, color: '#09131f' }}>
                        Host Session Control
                      </Typography>
                      <Typography variant="body2" sx={{ color: '#334e68', mt: 0.5 }}>
                        Restricted host JWT authorization with live participant management.
                      </Typography>
                    </Box>
                  </Stack>
                </Grid>
              </Grid>
            </Paper>
          </Box>
        </Container>
      </Box>
    </>
  );
}

export default HomePage;
