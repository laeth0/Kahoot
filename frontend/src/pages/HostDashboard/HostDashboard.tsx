import AddIcon from '@mui/icons-material/Add';
import PlayCircleOutlineIcon from '@mui/icons-material/PlayCircleOutline';
import QuizIcon from '@mui/icons-material/Quiz';
import SensorsIcon from '@mui/icons-material/Sensors';
import {
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  Chip,
  Container,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';

import { useAuth } from '../../hooks/useAuth.ts';

export function HostDashboard() {
  const { host } = useAuth();

  // Sample initial quiz list for host overview
  const sampleQuizzes = [
    {
      id: 'quiz-01',
      title: 'IEEEXtreme 19.0 Practice Warmup',
      description: 'Data structures, algorithms, and logic puzzles for competitive programmers.',
      questionsCount: 15,
      totalPoints: 1500,
      status: 'Ready to Host',
    },
    {
      id: 'quiz-02',
      title: 'Palestinian Tech & Innovation Trivia',
      description:
        'Highlighting top Palestinian engineering achievements and tech ecosystem milestones.',
      questionsCount: 10,
      totalPoints: 1000,
      status: 'Ready to Host',
    },
  ];

  return (
    <Box sx={{ flexGrow: 1, py: { xs: 4, md: 6 }, bgcolor: '#f4f8fc' }}>
      <Container maxWidth="lg">
        {/* Welcome Header */}
        <Paper
          elevation={0}
          sx={{
            p: { xs: 3, sm: 4 },
            mb: 4,
            borderRadius: 3,
            border: '1px solid #e2e8f0',
            bgcolor: '#ffffff',
            background:
              'radial-gradient(ellipse at 85% 20%, rgba(0, 98, 155, 0.08) 0%, #ffffff 70%)',
          }}
        >
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={3}
            sx={{ justifyContent: 'space-between', alignItems: { md: 'center' } }}
          >
            <Box>
              <Stack direction="row" spacing={1} sx={{ mb: 1.5, alignItems: 'center' }}>
                <Chip label="Host Session" size="small" color="primary" sx={{ fontWeight: 600 }} />
                <Chip
                  icon={<SensorsIcon sx={{ fontSize: '1rem !important' }} />}
                  label="Live System Online"
                  size="small"
                  color="secondary"
                  sx={{ fontWeight: 600 }}
                />
              </Stack>
              <Typography variant="h3" component="h1" sx={{ fontWeight: 800, color: '#09131f' }}>
                Welcome, {host?.username || 'Host'}!
              </Typography>
              <Typography variant="body1" sx={{ color: '#334e68', mt: 0.5 }}>
                Manage your quiz catalog, launch live sessions, and track real-time player
                scoreboards.
              </Typography>
            </Box>

            <Stack direction="row" spacing={2}>
              <Button
                variant="contained"
                color="primary"
                startIcon={<AddIcon />}
                sx={{ minHeight: 44, px: 2.5, fontWeight: 700 }}
              >
                Create Quiz
              </Button>
            </Stack>
          </Stack>
        </Paper>

        {/* Quizzes Grid */}
        <Box sx={{ mb: 3 }}>
          <Typography variant="h5" component="h2" sx={{ fontWeight: 700, color: '#09131f', mb: 2 }}>
            Your Quizzes
          </Typography>

          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: 'repeat(2, 1fr)' },
              gap: 3,
            }}
          >
            {sampleQuizzes.map((quiz) => (
              <Card
                key={quiz.id}
                elevation={0}
                sx={{
                  display: 'flex',
                  flexDirection: 'column',
                  borderRadius: 3,
                  border: '1px solid #e2e8f0',
                  transition: 'transform 0.2s ease, box-shadow 0.2s ease',
                  '&:hover': {
                    transform: 'translateY(-2px)',
                    boxShadow:
                      '0 12px 20px -5px rgba(9, 19, 31, 0.08), 0 4px 6px -2px rgba(9, 19, 31, 0.04)',
                  },
                }}
              >
                <CardContent sx={{ flexGrow: 1, p: 3 }}>
                  <Stack
                    direction="row"
                    sx={{ justifyContent: 'space-between', alignItems: 'flex-start', mb: 1.5 }}
                  >
                    <Chip
                      icon={<QuizIcon sx={{ fontSize: '1rem !important' }} />}
                      label={`${quiz.questionsCount} Questions`}
                      size="small"
                      sx={{ bgcolor: '#eef7fc', color: '#00629b', fontWeight: 600 }}
                    />
                    <Chip
                      label={quiz.status}
                      size="small"
                      color="primary"
                      variant="outlined"
                      sx={{ fontWeight: 600 }}
                    />
                  </Stack>

                  <Typography
                    variant="h6"
                    component="h3"
                    sx={{ fontWeight: 700, color: '#09131f', mb: 1 }}
                  >
                    {quiz.title}
                  </Typography>

                  <Typography variant="body2" sx={{ color: '#334e68', mb: 2 }}>
                    {quiz.description}
                  </Typography>

                  <Divider sx={{ my: 1.5 }} />

                  <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 500 }}>
                    Max Points: {quiz.totalPoints} pts
                  </Typography>
                </CardContent>

                <CardActions sx={{ p: 2, pt: 0, justifyContent: 'space-between' }}>
                  <Button size="small" sx={{ fontWeight: 600, color: '#334e68' }}>
                    Edit Questions
                  </Button>
                  <Button
                    variant="contained"
                    color="primary"
                    size="small"
                    startIcon={<PlayCircleOutlineIcon />}
                    sx={{ fontWeight: 700 }}
                  >
                    Start Game
                  </Button>
                </CardActions>
              </Card>
            ))}
          </Box>
        </Box>
      </Container>
    </Box>
  );
}

export default HostDashboard;
