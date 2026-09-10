import AddIcon from '@mui/icons-material/Add';
import CloseIcon from '@mui/icons-material/Close';
import SaveIcon from '@mui/icons-material/Save';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  Grid,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { type FormEvent, useState } from 'react';

import type { ChoiceInput, SaveQuestionPayload } from '../../api/quizQuestionService.ts';
import type { QuestionResponse } from '../../api/quizService.ts';
import { ChoiceEditorRow } from '../ChoiceEditorRow/ChoiceEditorRow.tsx';
import { ImageUploadField } from '../ImageUploadField/ImageUploadField.tsx';

export interface QuestionFormDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (payload: SaveQuestionPayload) => Promise<void>;
  initialData?: QuestionResponse | null;
  isSaving?: boolean;
}

const DEFAULT_CHOICES: ChoiceInput[] = [
  { text: '', imageUrl: null, isCorrect: true },
  { text: '', imageUrl: null, isCorrect: false },
  { text: '', imageUrl: null, isCorrect: false },
  { text: '', imageUrl: null, isCorrect: false },
];

const TIME_LIMIT_OPTIONS = [5, 10, 20, 30, 60, 90, 120, 240, 300];
const POINT_OPTIONS = [
  { label: 'Standard (1000 pts)', value: 1000 },
  { label: 'Double Points (2000 pts)', value: 2000 },
  { label: 'No Points (0 pts)', value: 0 },
];

interface QuestionFormContentProps {
  initialData?: QuestionResponse | null;
  onClose: () => void;
  onSubmit: (payload: SaveQuestionPayload) => Promise<void>;
  isSaving: boolean;
}

function QuestionFormContent({
  initialData,
  onClose,
  onSubmit,
  isSaving,
}: QuestionFormContentProps) {
  const [text, setText] = useState<string>(() => initialData?.text ?? '');
  const [imageUrl, setImageUrl] = useState<string | null>(() => initialData?.imageUrl ?? null);
  const [timeLimitSeconds, setTimeLimitSeconds] = useState<number>(
    () => initialData?.timeLimitSeconds ?? 20,
  );
  const [points, setPoints] = useState<number>(() => initialData?.points ?? 1000);
  const [choices, setChoices] = useState<ChoiceInput[]>(() =>
    initialData
      ? initialData.choices.map((c) => ({
          text: c.text,
          imageUrl: c.imageUrl,
          isCorrect: c.isCorrect,
        }))
      : DEFAULT_CHOICES,
  );
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleChoiceChange = (index: number, updated: ChoiceInput) => {
    setChoices((prev) => {
      const next = [...prev];
      next[index] = updated;
      return next;
    });
    if (validationError) setValidationError(null);
  };

  const handleToggleCorrect = (index: number) => {
    setChoices((prev) => prev.map((c, i) => (i === index ? { ...c, isCorrect: !c.isCorrect } : c)));
    if (validationError) setValidationError(null);
  };

  const handleAddChoice = () => {
    if (choices.length >= 6) return;
    setChoices((prev) => [...prev, { text: '', imageUrl: null, isCorrect: false }]);
  };

  const handleRemoveChoice = (index: number) => {
    if (choices.length <= 2) return;
    setChoices((prev) => {
      const next = prev.filter((_, i) => i !== index);
      if (!next.some((c) => c.isCorrect) && next.length > 0) {
        next[0].isCorrect = true;
      }
      return next;
    });
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    if (!text.trim()) {
      setValidationError('Question text is required.');
      return;
    }

    if (text.trim().length > 500) {
      setValidationError('Question text cannot exceed 500 characters.');
      return;
    }

    if (choices.length < 2 || choices.length > 6) {
      setValidationError('A question must have between 2 and 6 choices.');
      return;
    }

    const correctCount = choices.filter((c) => c.isCorrect).length;
    if (correctCount < 1) {
      setValidationError('Mark at least one choice as correct.');
      return;
    }

    const invalidChoice = choices.some((c) => (!c.text || !c.text.trim()) && !c.imageUrl);
    if (invalidChoice) {
      setValidationError('Every choice must have either answer text or an image.');
      return;
    }

    setValidationError(null);

    await onSubmit({
      text: text.trim(),
      imageUrl,
      timeLimitSeconds,
      points,
      choices,
    });
  };

  return (
    <>
      <DialogTitle
        id="question-dialog-title"
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          pb: 1.5,
        }}
      >
        <Typography variant="h6" component="span" sx={{ fontWeight: 800, color: '#09131f' }}>
          {initialData ? 'Edit Question' : 'Add New Question'}
        </Typography>
        <IconButton
          aria-label="Close question dialog"
          onClick={onClose}
          disabled={isSaving}
          size="small"
        >
          <CloseIcon />
        </IconButton>
      </DialogTitle>

      <Divider />

      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent sx={{ p: { xs: 2.5, sm: 3.5 } }}>
          <Stack spacing={3}>
            {validationError && (
              <Alert severity="error" role="alert">
                {validationError}
              </Alert>
            )}

            <Box>
              <Typography
                component="label"
                htmlFor="question-text-input"
                variant="subtitle2"
                sx={{ fontWeight: 700, color: '#09131f', mb: 1, display: 'block' }}
              >
                Question Text *
              </Typography>
              <TextField
                id="question-text-input"
                fullWidth
                multiline
                rows={3}
                placeholder="e.g. What is the time complexity of quicksort in the worst case?"
                value={text}
                onChange={(e) => {
                  setText(e.target.value.slice(0, 500));
                  if (validationError) setValidationError(null);
                }}
                required
                disabled={isSaving}
                slotProps={{
                  input: {
                    sx: {
                      fontWeight: 600,
                      fontSize: '1.05rem',
                      lineHeight: 1.5,
                      bgcolor: '#ffffff',
                    },
                  },
                }}
                helperText={`${text.length}/500 characters`}
              />
            </Box>

            <Grid container spacing={2}>
              <Grid size={{ xs: 12, sm: 6 }}>
                <FormControl fullWidth size="small">
                  <InputLabel id="time-limit-select-label">Time Limit</InputLabel>
                  <Select
                    labelId="time-limit-select-label"
                    value={timeLimitSeconds}
                    label="Time Limit"
                    onChange={(e) => setTimeLimitSeconds(Number(e.target.value))}
                    disabled={isSaving}
                  >
                    {TIME_LIMIT_OPTIONS.map((sec) => (
                      <MenuItem key={sec} value={sec}>
                        {sec} seconds
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Grid>

              <Grid size={{ xs: 12, sm: 6 }}>
                <FormControl fullWidth size="small">
                  <InputLabel id="points-select-label">Points</InputLabel>
                  <Select
                    labelId="points-select-label"
                    value={points}
                    label="Points"
                    onChange={(e) => setPoints(Number(e.target.value))}
                    disabled={isSaving}
                  >
                    {POINT_OPTIONS.map((opt) => (
                      <MenuItem key={opt.value} value={opt.value}>
                        {opt.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Grid>
            </Grid>

            <Box>
              <ImageUploadField
                value={imageUrl}
                onChange={(url) => setImageUrl(url)}
                label="Question Image / Diagram (optional)"
                disabled={isSaving}
              />
            </Box>

            <Divider />

            <Box>
              <Stack
                direction="row"
                sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 2 }}
              >
                <Box>
                  <Typography variant="subtitle1" sx={{ fontWeight: 800, color: '#09131f' }}>
                    Answer Choices ({choices.length}/6)
                  </Typography>
                  <Typography variant="caption" sx={{ color: '#334e68' }}>
                    Tick every choice that should count as a correct answer
                  </Typography>
                </Box>

                {choices.length < 6 && (
                  <Button
                    variant="outlined"
                    size="small"
                    startIcon={<AddIcon />}
                    onClick={handleAddChoice}
                    disabled={isSaving}
                    sx={{ fontWeight: 600 }}
                  >
                    Add Choice
                  </Button>
                )}
              </Stack>

              <Grid container spacing={2}>
                {choices.map((choice, index) => (
                  <Grid key={index} size={{ xs: 12, md: 6 }}>
                    <ChoiceEditorRow
                      index={index}
                      choice={choice}
                      onChange={(updated) => handleChoiceChange(index, updated)}
                      onRemove={() => handleRemoveChoice(index)}
                      onToggleCorrect={() => handleToggleCorrect(index)}
                      canRemove={choices.length > 2}
                      disabled={isSaving}
                    />
                  </Grid>
                ))}
              </Grid>
            </Box>
          </Stack>
        </DialogContent>

        <Divider />

        <DialogActions sx={{ p: 2.5, gap: 1 }}>
          <Button
            onClick={onClose}
            disabled={isSaving}
            variant="outlined"
            color="inherit"
            sx={{ minHeight: 44, fontWeight: 600 }}
          >
            Cancel
          </Button>

          <Button
            type="submit"
            variant="contained"
            color="primary"
            disabled={isSaving}
            startIcon={isSaving ? <CircularProgress size={18} color="inherit" /> : <SaveIcon />}
            sx={{ minHeight: 44, fontWeight: 700, px: 3 }}
          >
            {isSaving ? 'Saving Question...' : 'Save Question'}
          </Button>
        </DialogActions>
      </Box>
    </>
  );
}

export function QuestionFormDialog({
  open,
  onClose,
  onSubmit,
  initialData,
  isSaving = false,
}: QuestionFormDialogProps) {
  const theme = useTheme();
  const isFullScreen = useMediaQuery(theme.breakpoints.down('md'));

  return (
    <Dialog
      open={open}
      onClose={isSaving ? undefined : onClose}
      fullScreen={isFullScreen}
      maxWidth="md"
      fullWidth
      aria-labelledby="question-dialog-title"
      slotProps={{
        paper: {
          sx: {
            borderRadius: isFullScreen ? 0 : 3,
          },
        },
      }}
    >
      {open && (
        <QuestionFormContent
          key={initialData?.id ?? 'create'}
          initialData={initialData}
          onClose={onClose}
          onSubmit={onSubmit}
          isSaving={isSaving}
        />
      )}
    </Dialog>
  );
}

export default QuestionFormDialog;
