import AddIcon from '@mui/icons-material/Add';
import ClearIcon from '@mui/icons-material/Clear';
import RefreshIcon from '@mui/icons-material/Refresh';
import SearchIcon from '@mui/icons-material/Search';
import {
  Alert,
  Box,
  Button,
  Container,
  Grid,
  IconButton,
  InputAdornment,
  Paper,
  Snackbar,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { hostGameService } from '../../api/hostGameService.ts';
import type { QuizSummaryResponse } from '../../api/quizService.ts';
import { ConfirmDialog } from '../../components/ConfirmDialog/ConfirmDialog.tsx';
import { ErrorState, LoadingState } from '../../components/Feedback/index.ts';
import { MetadataManager } from '../../components/MetadataManager/MetadataManager.tsx';
import { QuizCard } from '../../components/QuizCard/QuizCard.tsx';
import { useQuizzes } from '../../hooks/useQuizzes.ts';

type FilterTab = 'all' | 'published' | 'draft';

export function QuizLibraryPage() {
  const navigate = useNavigate();
  const { quizzes, isLoading, error, refetch, deleteQuiz } = useQuizzes();

  const [searchQuery, setSearchQuery] = useState('');
  const [filterTab, setFilterTab] = useState<FilterTab>('all');
  const [quizToDelete, setQuizToDelete] = useState<QuizSummaryResponse | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [startingQuizId, setStartingQuizId] = useState<string | null>(null);
  const [snackbarMessage, setSnackbarMessage] = useState<string | null>(null);
  const [snackbarSeverity, setSnackbarSeverity] = useState<'success' | 'error'>('success');

  const filteredQuizzes = useMemo(() => {
    return quizzes.filter((quiz) => {
      const matchesSearch =
        quiz.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
        (quiz.description && quiz.description.toLowerCase().includes(searchQuery.toLowerCase()));

      if (!matchesSearch) return false;

      if (filterTab === 'published') return quiz.isPublished;
      if (filterTab === 'draft') return !quiz.isPublished;
      return true;
    });
  }, [quizzes, searchQuery, filterTab]);

  const handleEditQuiz = (quizId: string) => {
    navigate(`/host/quizzes/${quizId}`);
  };

  const handleStartGame = async (quizId: string) => {
    setStartingQuizId(quizId);
    try {
      const response = await hostGameService.createGame(quizId);
      setSnackbarSeverity('success');
      setSnackbarMessage(`Game created with PIN: ${response.pin}. Launching lobby...`);
      navigate(`/host/game/${response.gameId}`, { state: { quizId } });
    } catch (err) {
      setSnackbarSeverity('error');
      const msg = err instanceof Error ? err.message : 'Failed to launch game session';
      setSnackbarMessage(msg);
    } finally {
      setStartingQuizId(null);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!quizToDelete) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await deleteQuiz(quizToDelete.id);
      setQuizToDelete(null);
      setSnackbarSeverity('success');
      setSnackbarMessage('Quiz deleted successfully');
    } catch (err) {
      const msg =
        err instanceof Error
          ? err.message
          : 'Unable to delete quiz. It may be part of an active session or historical records.';
      setDeleteError(msg);
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: '#f4f8fc', py: { xs: 4, md: 6 } }}>
      <MetadataManager
        title="Quiz Library - Kahoot"
        description="Manage, create, and launch your interactive quizzes."
      />

      <Container maxWidth="lg">
        <Stack spacing={4}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={2}
            sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}
          >
            <Box>
              <Typography
                variant="h4"
                component="h1"
                sx={{
                  fontWeight: 800,
                  color: '#09131f',
                  letterSpacing: '-0.02em',
                  mb: 0.5,
                }}
              >
                My Quizzes
              </Typography>
              <Typography variant="body1" sx={{ color: '#486581' }}>
                Create, customize, and host interactive quizzes for your participants.
              </Typography>
            </Box>

            <Button
              variant="contained"
              color="primary"
              size="large"
              startIcon={<AddIcon />}
              onClick={() => navigate('/host/quizzes/new')}
              sx={{
                fontWeight: 700,
                borderRadius: 2.5,
                px: 3,
                py: 1.25,
                alignSelf: { xs: 'flex-start', sm: 'center' },
              }}
            >
              Create Quiz
            </Button>
          </Stack>

          {error ? (
            <ErrorState
              title="Unable to load quizzes"
              message={error}
              onRetry={refetch}
              retryText="Retry"
            />
          ) : isLoading ? (
            <LoadingState message="Loading your quizzes..." minHeight={320} />
          ) : (
            <>
              <Stack
                direction={{ xs: 'column', md: 'row' }}
                spacing={2}
                sx={{ justifyContent: 'space-between', alignItems: { md: 'center' } }}
              >
                <TextField
                  placeholder="Search quizzes by title or description..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  size="small"
                  sx={{
                    width: { xs: '100%', md: 380 },
                    bgcolor: '#ffffff',
                    borderRadius: 2,
                    '& .MuiOutlinedInput-root': {
                      borderRadius: 2,
                    },
                  }}
                  slotProps={{
                    input: {
                      startAdornment: (
                        <InputAdornment position="start">
                          <SearchIcon sx={{ color: '#627d98', fontSize: 20 }} />
                        </InputAdornment>
                      ),
                      endAdornment: searchQuery ? (
                        <InputAdornment position="end">
                          <IconButton
                            size="small"
                            onClick={() => setSearchQuery('')}
                            aria-label="Clear search input"
                          >
                            <ClearIcon sx={{ fontSize: 16 }} />
                          </IconButton>
                        </InputAdornment>
                      ) : null,
                    },
                  }}
                />

                <Stack
                  direction="row"
                  spacing={2}
                  sx={{ alignItems: 'center', width: { xs: '100%', md: 'auto' } }}
                >
                  <ToggleButtonGroup
                    value={filterTab}
                    exclusive
                    onChange={(_, val) => {
                      if (val) setFilterTab(val);
                    }}
                    size="small"
                    aria-label="Filter quizzes by publication status"
                    sx={{
                      bgcolor: '#ffffff',
                      borderRadius: 2,
                      '& .MuiToggleButton-root': {
                        fontWeight: 600,
                        textTransform: 'none',
                        px: 2,
                        borderColor: '#cbd5e1',
                        '&.Mui-selected': {
                          bgcolor: '#00629b',
                          color: '#ffffff',
                          '&:hover': {
                            bgcolor: '#004c78',
                          },
                        },
                      },
                    }}
                  >
                    <ToggleButton value="all" aria-label="All quizzes">
                      All ({quizzes.length})
                    </ToggleButton>
                    <ToggleButton value="published" aria-label="Published quizzes">
                      Published ({quizzes.filter((q) => q.isPublished).length})
                    </ToggleButton>
                    <ToggleButton value="draft" aria-label="Draft quizzes">
                      Drafts ({quizzes.filter((q) => !q.isPublished).length})
                    </ToggleButton>
                  </ToggleButtonGroup>

                  <IconButton
                    onClick={refetch}
                    aria-label="Refresh quizzes list"
                    sx={{
                      bgcolor: '#ffffff',
                      border: '1px solid #cbd5e1',
                      borderRadius: 2,
                      color: '#486581',
                      '&:hover': { bgcolor: '#f0f4f8' },
                    }}
                  >
                    <RefreshIcon fontSize="small" />
                  </IconButton>
                </Stack>
              </Stack>

              {filteredQuizzes.length === 0 ? (
                <Paper
                  elevation={0}
                  sx={{
                    p: { xs: 4, sm: 8 },
                    textAlign: 'center',
                    bgcolor: '#ffffff',
                    borderRadius: 3,
                    border: '1px dashed #cbd5e1',
                  }}
                >
                  <Typography variant="h6" sx={{ fontWeight: 800, color: '#09131f', mb: 1 }}>
                    {searchQuery ? 'No matching quizzes found' : 'No quizzes yet'}
                  </Typography>
                  <Typography
                    variant="body2"
                    sx={{ color: '#486581', maxWidth: 440, mx: 'auto', mb: 3 }}
                  >
                    {searchQuery
                      ? `We couldn't find any quizzes matching "${searchQuery}". Try adjusting your search query or clear the filter.`
                      : 'Create your first quiz to start hosting engaging real-time competitions for your audience.'}
                  </Typography>

                  {searchQuery ? (
                    <Button
                      variant="outlined"
                      onClick={() => setSearchQuery('')}
                      sx={{ fontWeight: 700 }}
                    >
                      Clear Search
                    </Button>
                  ) : (
                    <Button
                      variant="contained"
                      color="primary"
                      startIcon={<AddIcon />}
                      onClick={() => navigate('/host/quizzes/new')}
                      sx={{ fontWeight: 700 }}
                    >
                      Create First Quiz
                    </Button>
                  )}
                </Paper>
              ) : (
                <Grid container spacing={3}>
                  {filteredQuizzes.map((quiz) => (
                    <Grid key={quiz.id} size={{ xs: 12, sm: 6, lg: 4 }}>
                      <QuizCard
                        quiz={quiz}
                        onEdit={handleEditQuiz}
                        onStartGame={handleStartGame}
                        onDelete={(q) => {
                          setDeleteError(null);
                          setQuizToDelete(q);
                        }}
                        isStartingGame={startingQuizId === quiz.id}
                      />
                    </Grid>
                  ))}
                </Grid>
              )}
            </>
          )}
        </Stack>
      </Container>

      <ConfirmDialog
        open={Boolean(quizToDelete)}
        title="Delete Quiz?"
        message={
          quizToDelete
            ? `Are you sure you want to permanently delete "${quizToDelete.title}"? This action cannot be undone.`
            : ''
        }
        confirmText="Delete Quiz"
        cancelText="Cancel"
        severity="error"
        isLoading={isDeleting}
        onConfirm={handleDeleteConfirm}
        onCancel={() => {
          if (!isDeleting) {
            setQuizToDelete(null);
            setDeleteError(null);
          }
        }}
      >
        {deleteError && (
          <Box sx={{ mt: 2 }}>
            <Alert severity="error">{deleteError}</Alert>
          </Box>
        )}
      </ConfirmDialog>

      <Snackbar
        open={Boolean(snackbarMessage)}
        autoHideDuration={5000}
        onClose={() => setSnackbarMessage(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity={snackbarSeverity}
          onClose={() => setSnackbarMessage(null)}
          sx={{ width: '100%', fontWeight: 600 }}
        >
          {snackbarMessage}
        </Alert>
      </Snackbar>
    </Box>
  );
}

export default QuizLibraryPage;
