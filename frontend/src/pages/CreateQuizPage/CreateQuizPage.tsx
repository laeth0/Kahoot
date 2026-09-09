import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import QuizIcon from '@mui/icons-material/Quiz';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Container,
  Divider,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { type FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { quizService } from '../../api/quizService.ts';
import { MetadataManager } from '../../components/MetadataManager/MetadataManager.tsx';

export function CreateQuizPage() {
  const navigate = useNavigate();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      setErrorMessage('Quiz title is required.');
      return;
    }

    if (trimmedTitle.length > 200) {
      setErrorMessage('Quiz title cannot exceed 200 characters.');
      return;
    }

    if (description.trim().length > 1000) {
      setErrorMessage('Quiz description cannot exceed 1000 characters.');
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      const quizId = await quizService.createQuiz({
        title: trimmedTitle,
        description: description.trim() || null,
      });

      navigate(`/host/quizzes/${quizId}`);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to create quiz';
      setErrorMessage(msg);
      setIsSubmitting(false);
    }
  };

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: '#f4f8fc', py: { xs: 4, md: 6 } }}>
      <MetadataManager
        title="Create New Quiz - Kahoot"
        description="Configure your new interactive quiz title and description."
      />

      <Container maxWidth="md">
        <Stack spacing={4}>
          <Box>
            <Button
              startIcon={<ArrowBackIcon />}
              onClick={() => navigate('/host/quizzes')}
              sx={{
                fontWeight: 600,
                color: '#486581',
                mb: 2,
                textTransform: 'none',
                '&:hover': {
                  color: '#00629b',
                  bgcolor: 'transparent',
                },
              }}
            >
              Back to My Quizzes
            </Button>

            <Typography
              variant="h4"
              component="h1"
              sx={{
                fontWeight: 800,
                color: '#09131f',
                letterSpacing: '-0.02em',
                mb: 1,
              }}
            >
              Create New Quiz
            </Typography>
            <Typography variant="body1" sx={{ color: '#486581' }}>
              Enter the title and optional overview for your quiz. You will add questions and set
              timers on the next screen.
            </Typography>
          </Box>

          <Card
            variant="outlined"
            sx={{
              borderRadius: 3,
              borderColor: '#e2e8f0',
              bgcolor: '#ffffff',
              boxShadow: '0 4px 20px rgba(0, 98, 155, 0.04)',
            }}
          >
            <CardContent sx={{ p: { xs: 3, sm: 4.5 } }}>
              <Box component="form" onSubmit={handleSubmit} noValidate>
                <Stack spacing={3.5}>
                  {errorMessage && (
                    <Alert severity="error" role="alert">
                      {errorMessage}
                    </Alert>
                  )}

                  <Box>
                    <Typography
                      component="label"
                      htmlFor="quiz-title-input"
                      variant="subtitle2"
                      sx={{ fontWeight: 700, color: '#09131f', mb: 1, display: 'block' }}
                    >
                      Quiz Title *
                    </Typography>
                    <TextField
                      id="quiz-title-input"
                      fullWidth
                      placeholder="e.g. Data Structures & Algorithms Championship"
                      value={title}
                      onChange={(e) => {
                        setTitle(e.target.value.slice(0, 200));
                        if (errorMessage) setErrorMessage(null);
                      }}
                      required
                      disabled={isSubmitting}
                      slotProps={{
                        input: {
                          sx: {
                            fontWeight: 600,
                            fontSize: '1.05rem',
                          },
                        },
                      }}
                      helperText={`${title.length}/200 characters`}
                    />
                  </Box>

                  <Box>
                    <Typography
                      component="label"
                      htmlFor="quiz-description-input"
                      variant="subtitle2"
                      sx={{ fontWeight: 700, color: '#09131f', mb: 1, display: 'block' }}
                    >
                      Description (Optional)
                    </Typography>
                    <TextField
                      id="quiz-description-input"
                      fullWidth
                      multiline
                      rows={4}
                      placeholder="Provide context, instructions, or rules for players participating in this quiz session."
                      value={description}
                      onChange={(e) => {
                        setDescription(e.target.value.slice(0, 1000));
                        if (errorMessage) setErrorMessage(null);
                      }}
                      disabled={isSubmitting}
                      helperText={`${description.length}/1000 characters`}
                    />
                  </Box>

                  <Divider />

                  <Stack
                    direction={{ xs: 'column-reverse', sm: 'row' }}
                    spacing={2}
                    sx={{ justifyContent: 'flex-end', pt: 1 }}
                  >
                    <Button
                      variant="outlined"
                      color="inherit"
                      onClick={() => navigate('/host/quizzes')}
                      disabled={isSubmitting}
                      sx={{
                        fontWeight: 600,
                        minHeight: 46,
                        px: 3,
                        borderColor: '#cbd5e1',
                      }}
                    >
                      Cancel
                    </Button>

                    <Button
                      type="submit"
                      variant="contained"
                      color="primary"
                      disabled={isSubmitting || !title.trim()}
                      startIcon={
                        isSubmitting ? <CircularProgress size={18} color="inherit" /> : <QuizIcon />
                      }
                      sx={{
                        fontWeight: 700,
                        minHeight: 46,
                        px: 3.5,
                      }}
                    >
                      {isSubmitting ? 'Creating Quiz...' : 'Create & Add Questions'}
                    </Button>
                  </Stack>
                </Stack>
              </Box>
            </CardContent>
          </Card>
        </Stack>
      </Container>
    </Box>
  );
}

export default CreateQuizPage;
