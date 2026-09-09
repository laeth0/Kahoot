import AddIcon from '@mui/icons-material/Add';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import EditIcon from '@mui/icons-material/Edit';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import PublishIcon from '@mui/icons-material/Publish';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  Snackbar,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { type FormEvent, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { hostGameService } from '../../api/hostGameService.ts';
import type { SaveQuestionPayload } from '../../api/quizQuestionService.ts';
import type { QuestionResponse } from '../../api/quizService.ts';
import { ConfirmDialog } from '../../components/ConfirmDialog/ConfirmDialog.tsx';
import { ErrorState, LoadingState } from '../../components/Feedback/index.ts';
import { MetadataManager } from '../../components/MetadataManager/MetadataManager.tsx';
import {
  evaluateQuizPublishCriteria,
  PublishChecklist,
} from '../../components/PublishChecklist/index.ts';
import { QuestionFormDialog } from '../../components/QuestionForm/QuestionFormDialog.tsx';
import { ReorderableQuestionList } from '../../components/ReorderableQuestionList/ReorderableQuestionList.tsx';
import { useQuiz } from '../../hooks/useQuiz.ts';

export function QuizEditorPage() {
  const { quizId } = useParams<{ quizId: string }>();
  const navigate = useNavigate();

  const {
    quiz,
    isLoading,
    error,
    isMutating,
    refetch,
    updateMetadata,
    publish,
    addQuestion,
    updateQuestion,
    deleteQuestion,
    reorderQuestions,
  } = useQuiz(quizId);

  const [questionModalOpen, setQuestionModalOpen] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<QuestionResponse | null>(null);
  const [questionToDelete, setQuestionToDelete] = useState<string | null>(null);
  const [isDeletingQuestion, setIsDeletingQuestion] = useState(false);

  const [metadataModalOpen, setMetadataModalOpen] = useState(false);
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [metadataError, setMetadataError] = useState<string | null>(null);

  const [isStartingGame, setIsStartingGame] = useState(false);
  const [snackbarMessage, setSnackbarMessage] = useState<string | null>(null);
  const [snackbarSeverity, setSnackbarSeverity] = useState<'success' | 'error'>('success');

  const { isPublishable } = evaluateQuizPublishCriteria(quiz?.questions || []);

  const handleOpenMetadataModal = () => {
    if (!quiz) return;
    setEditTitle(quiz.title);
    setEditDescription(quiz.description || '');
    setMetadataError(null);
    setMetadataModalOpen(true);
  };

  const handleSaveMetadata = async (e: FormEvent) => {
    e.preventDefault();
    const trimmedTitle = editTitle.trim();
    if (!trimmedTitle) {
      setMetadataError('Quiz title is required.');
      return;
    }

    try {
      await updateMetadata({
        title: trimmedTitle,
        description: editDescription.trim() || null,
      });
      setMetadataModalOpen(false);
      setSnackbarSeverity('success');
      setSnackbarMessage('Quiz details updated. The quiz is now in draft mode until re-published.');
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to update quiz details';
      setMetadataError(msg);
    }
  };

  const handlePublishQuiz = async () => {
    try {
      await publish();
      setSnackbarSeverity('success');
      setSnackbarMessage('Quiz published successfully! It is now ready to be hosted.');
    } catch (err) {
      setSnackbarSeverity('error');
      const msg = err instanceof Error ? err.message : 'Failed to publish quiz';
      setSnackbarMessage(msg);
    }
  };

  const handleStartGame = async () => {
    if (!quiz) return;
    setIsStartingGame(true);
    try {
      const res = await hostGameService.createGame(quiz.id);
      setSnackbarSeverity('success');
      setSnackbarMessage(`Game created with PIN: ${res.pin}. Redirecting to game lobby...`);
      navigate(`/host/game/${res.gameId}`);
    } catch (err) {
      setSnackbarSeverity('error');
      const msg = err instanceof Error ? err.message : 'Failed to create game session';
      setSnackbarMessage(msg);
    } finally {
      setIsStartingGame(false);
    }
  };

  const handleOpenAddQuestion = () => {
    setEditingQuestion(null);
    setQuestionModalOpen(true);
  };

  const handleOpenEditQuestion = (question: QuestionResponse) => {
    setEditingQuestion(question);
    setQuestionModalOpen(true);
  };

  const handleSaveQuestion = async (payload: SaveQuestionPayload) => {
    try {
      if (editingQuestion) {
        await updateQuestion(editingQuestion.id, payload);
        setSnackbarSeverity('success');
        setSnackbarMessage('Question updated successfully.');
      } else {
        await addQuestion(payload);
        setSnackbarSeverity('success');
        setSnackbarMessage('Question added successfully.');
      }
      setQuestionModalOpen(false);
    } catch (err) {
      setSnackbarSeverity('error');
      const msg = err instanceof Error ? err.message : 'Failed to save question';
      setSnackbarMessage(msg);
    }
  };

  const handleDeleteQuestionConfirm = async () => {
    if (!questionToDelete) return;
    setIsDeletingQuestion(true);
    try {
      await deleteQuestion(questionToDelete);
      setQuestionToDelete(null);
      setSnackbarSeverity('success');
      setSnackbarMessage('Question removed.');
    } catch (err) {
      setSnackbarSeverity('error');
      const msg = err instanceof Error ? err.message : 'Failed to delete question';
      setSnackbarMessage(msg);
    } finally {
      setIsDeletingQuestion(false);
    }
  };

  const handleMoveUp = async (index: number) => {
    if (!quiz || index <= 0) return;
    const sorted = [...quiz.questions].sort((a, b) => a.orderIndex - b.orderIndex);
    const temp = sorted[index];
    sorted[index] = sorted[index - 1];
    sorted[index - 1] = temp;

    const orderedIds = sorted.map((q) => q.id);
    await reorderQuestions(orderedIds);
  };

  const handleMoveDown = async (index: number) => {
    if (!quiz || index >= quiz.questions.length - 1) return;
    const sorted = [...quiz.questions].sort((a, b) => a.orderIndex - b.orderIndex);
    const temp = sorted[index];
    sorted[index] = sorted[index + 1];
    sorted[index + 1] = temp;

    const orderedIds = sorted.map((q) => q.id);
    await reorderQuestions(orderedIds);
  };

  if (isLoading) {
    return (
      <Box sx={{ minHeight: '100vh', bgcolor: '#f4f8fc', py: 6 }}>
        <LoadingState message="Loading quiz details..." minHeight={400} />
      </Box>
    );
  }

  if (error || !quiz) {
    return (
      <Box sx={{ minHeight: '100vh', bgcolor: '#f4f8fc', py: 6 }}>
        <Container maxWidth="md">
          <ErrorState
            title="Quiz not found"
            message={error || 'Unable to load the requested quiz.'}
            onRetry={refetch}
            retryText="Retry"
          />
        </Container>
      </Box>
    );
  }

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: '#f4f8fc', py: { xs: 3, md: 5 } }}>
      <MetadataManager
        title={`${quiz.title} - Quiz Editor - Kahoot`}
        description={
          quiz.description || 'Edit questions, configure time limits, and publish your quiz.'
        }
      />

      <Container maxWidth="lg">
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

            <Card
              variant="outlined"
              sx={{
                borderRadius: 3,
                borderColor: '#e2e8f0',
                bgcolor: '#ffffff',
                p: { xs: 2.5, sm: 3.5 },
              }}
            >
              <Stack
                direction={{ xs: 'column', md: 'row' }}
                spacing={3}
                sx={{ justifyContent: 'space-between', alignItems: { md: 'flex-start' } }}
              >
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 1 }}>
                    <Chip
                      size="small"
                      label={quiz.isPublished ? 'Published' : 'Draft'}
                      sx={{
                        fontWeight: 700,
                        fontSize: '0.75rem',
                        bgcolor: quiz.isPublished
                          ? 'rgba(16, 185, 129, 0.12)'
                          : 'rgba(245, 158, 11, 0.12)',
                        color: quiz.isPublished ? '#065f46' : '#92400e',
                        border: '1px solid',
                        borderColor: quiz.isPublished
                          ? 'rgba(16, 185, 129, 0.3)'
                          : 'rgba(245, 158, 11, 0.3)',
                      }}
                    />
                    <Chip
                      size="small"
                      label={`${quiz.questions.length} ${
                        quiz.questions.length === 1 ? 'question' : 'questions'
                      }`}
                      sx={{ bgcolor: '#f0f4f8', color: '#334e68', fontWeight: 600 }}
                    />
                  </Stack>

                  <Typography
                    variant="h4"
                    component="h1"
                    sx={{
                      fontWeight: 800,
                      color: '#09131f',
                      letterSpacing: '-0.02em',
                      mb: 1,
                      wordBreak: 'break-word',
                    }}
                  >
                    {quiz.title}
                  </Typography>

                  <Typography
                    variant="body1"
                    sx={{
                      color: '#486581',
                      lineHeight: 1.5,
                      wordBreak: 'break-word',
                      mb: 2,
                    }}
                  >
                    {quiz.description || 'No description provided.'}
                  </Typography>

                  <Button
                    size="small"
                    variant="outlined"
                    startIcon={<EditIcon />}
                    onClick={handleOpenMetadataModal}
                    sx={{
                      fontWeight: 600,
                      borderRadius: 2,
                      borderColor: '#cbd5e1',
                      color: '#00629b',
                    }}
                  >
                    Edit Title & Description
                  </Button>
                </Box>

                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={1.5}
                  sx={{
                    alignItems: 'stretch',
                    alignSelf: { xs: 'stretch', md: 'flex-start' },
                    flexShrink: 0,
                  }}
                >
                  <Tooltip
                    title={
                      quiz.isPublished
                        ? 'This quiz is already published and ready for hosting.'
                        : !isPublishable
                          ? 'Resolve checklist requirements before publishing.'
                          : 'Publish quiz so it can be hosted in live sessions.'
                    }
                  >
                    <span>
                      <Button
                        variant="outlined"
                        color="success"
                        startIcon={
                          quiz.isPublished ? (
                            <CheckCircleIcon />
                          ) : isMutating ? (
                            <CircularProgress size={18} color="inherit" />
                          ) : (
                            <PublishIcon />
                          )
                        }
                        onClick={handlePublishQuiz}
                        disabled={quiz.isPublished || !isPublishable || isMutating}
                        sx={{
                          fontWeight: 700,
                          borderRadius: 2,
                          px: 2.5,
                          minHeight: 44,
                          width: '100%',
                        }}
                      >
                        {quiz.isPublished ? 'Published' : 'Publish Quiz'}
                      </Button>
                    </span>
                  </Tooltip>

                  <Tooltip
                    title={
                      !quiz.isPublished
                        ? 'Publish the quiz first before launching a live session.'
                        : 'Start a live game session with a unique PIN.'
                    }
                  >
                    <span>
                      <Button
                        variant="contained"
                        color="primary"
                        startIcon={
                          isStartingGame ? (
                            <CircularProgress size={18} color="inherit" />
                          ) : (
                            <PlayArrowIcon />
                          )
                        }
                        onClick={handleStartGame}
                        disabled={
                          !quiz.isPublished || quiz.questions.length === 0 || isStartingGame
                        }
                        sx={{
                          fontWeight: 700,
                          borderRadius: 2,
                          px: 3,
                          minHeight: 44,
                          width: '100%',
                        }}
                      >
                        Host Live Game
                      </Button>
                    </span>
                  </Tooltip>
                </Stack>
              </Stack>
            </Card>
          </Box>

          <Grid container spacing={3.5}>
            <Grid size={{ xs: 12, lg: 8 }}>
              <Stack spacing={2.5}>
                <Stack
                  direction="row"
                  sx={{ justifyContent: 'space-between', alignItems: 'center' }}
                >
                  <Box>
                    <Typography variant="h5" sx={{ fontWeight: 800, color: '#09131f' }}>
                      Questions ({quiz.questions.length})
                    </Typography>
                    <Typography variant="body2" sx={{ color: '#486581' }}>
                      Add and reorder questions with answer choices and point values.
                    </Typography>
                  </Box>

                  <Button
                    variant="contained"
                    color="primary"
                    startIcon={<AddIcon />}
                    onClick={handleOpenAddQuestion}
                    sx={{
                      fontWeight: 700,
                      borderRadius: 2.5,
                      px: 2.5,
                    }}
                  >
                    Add Question
                  </Button>
                </Stack>

                <ReorderableQuestionList
                  questions={quiz.questions}
                  onEdit={handleOpenEditQuestion}
                  onDelete={(id) => setQuestionToDelete(id)}
                  onMoveUp={handleMoveUp}
                  onMoveDown={handleMoveDown}
                  isReordering={isMutating}
                />
              </Stack>
            </Grid>

            <Grid size={{ xs: 12, lg: 4 }}>
              <Stack spacing={3}>
                <PublishChecklist questions={quiz.questions} />

                <Card
                  variant="outlined"
                  sx={{
                    borderRadius: 2.5,
                    borderColor: '#e2e8f0',
                    bgcolor: '#ffffff',
                  }}
                >
                  <CardContent sx={{ p: 2.5 }}>
                    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start' }}>
                      <InfoOutlinedIcon sx={{ color: '#00629b', fontSize: 20, mt: 0.2 }} />
                      <Box>
                        <Typography
                          variant="subtitle2"
                          sx={{ fontWeight: 700, color: '#09131f', mb: 0.5 }}
                        >
                          Draft Behavior Note
                        </Typography>
                        <Typography variant="caption" sx={{ color: '#486581', lineHeight: 1.5 }}>
                          Any edits made to questions or quiz details automatically switch the quiz
                          to Draft status. You must click "Publish Quiz" when ready so hosts can
                          launch new live game sessions.
                        </Typography>
                      </Box>
                    </Stack>
                  </CardContent>
                </Card>
              </Stack>
            </Grid>
          </Grid>
        </Stack>
      </Container>

      <QuestionFormDialog
        open={questionModalOpen}
        onClose={() => setQuestionModalOpen(false)}
        onSubmit={handleSaveQuestion}
        initialData={editingQuestion}
        isSaving={isMutating}
      />

      <Dialog
        open={metadataModalOpen}
        onClose={() => setMetadataModalOpen(false)}
        maxWidth="sm"
        fullWidth
        PaperProps={{ sx: { borderRadius: 3 } }}
      >
        <DialogTitle sx={{ fontWeight: 800, color: '#09131f' }}>Edit Quiz Details</DialogTitle>
        <Divider />
        <Box component="form" onSubmit={handleSaveMetadata} noValidate>
          <DialogContent sx={{ p: 3 }}>
            <Stack spacing={2.5}>
              {metadataError && (
                <Alert severity="error" role="alert">
                  {metadataError}
                </Alert>
              )}

              <Box>
                <Typography
                  component="label"
                  htmlFor="edit-quiz-title"
                  variant="subtitle2"
                  sx={{ fontWeight: 700, color: '#09131f', mb: 1, display: 'block' }}
                >
                  Quiz Title *
                </Typography>
                <TextField
                  id="edit-quiz-title"
                  fullWidth
                  value={editTitle}
                  onChange={(e) => setEditTitle(e.target.value.slice(0, 200))}
                  required
                  disabled={isMutating}
                  helperText={`${editTitle.length}/200 characters`}
                />
              </Box>

              <Box>
                <Typography
                  component="label"
                  htmlFor="edit-quiz-description"
                  variant="subtitle2"
                  sx={{ fontWeight: 700, color: '#09131f', mb: 1, display: 'block' }}
                >
                  Description
                </Typography>
                <TextField
                  id="edit-quiz-description"
                  fullWidth
                  multiline
                  rows={4}
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value.slice(0, 1000))}
                  disabled={isMutating}
                  helperText={`${editDescription.length}/1000 characters`}
                />
              </Box>
            </Stack>
          </DialogContent>
          <Divider />
          <DialogActions sx={{ p: 2.5 }}>
            <Button
              onClick={() => setMetadataModalOpen(false)}
              disabled={isMutating}
              variant="outlined"
              color="inherit"
              sx={{ fontWeight: 600 }}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              variant="contained"
              color="primary"
              disabled={isMutating || !editTitle.trim()}
              sx={{ fontWeight: 700, px: 3 }}
            >
              {isMutating ? 'Saving...' : 'Save Changes'}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>

      <ConfirmDialog
        open={Boolean(questionToDelete)}
        title="Delete Question?"
        message="Are you sure you want to delete this question? This action cannot be undone."
        confirmText="Delete Question"
        cancelText="Cancel"
        severity="error"
        isLoading={isDeletingQuestion}
        onConfirm={handleDeleteQuestionConfirm}
        onCancel={() => {
          if (!isDeletingQuestion) {
            setQuestionToDelete(null);
          }
        }}
      />

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

export default QuizEditorPage;
