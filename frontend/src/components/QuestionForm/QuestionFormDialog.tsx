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
  InputAdornment,
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

interface DraftChoice extends ChoiceInput {
  draftId: string;
}

const DEFAULT_CHOICES: DraftChoice[] = [
  { draftId: 'default-1', text: '', isCorrect: true },
  { draftId: 'default-2', text: '', isCorrect: false },
  { draftId: 'default-3', text: '', isCorrect: false },
  { draftId: 'default-4', text: '', isCorrect: false },
];

const TIME_LIMIT_OPTIONS = [5, 10, 20, 30, 60, 90, 120, 240, 300];
const PRESET_POINTS = [1000, 2000, 0] as const;

function validateCustomPoints(input: string): string | null {
  const trimmed = input.trim();
  if (!trimmed) {
    return 'Point value is required.';
  }
  if (trimmed.includes('-')) {
    return 'Points must be non-negative.';
  }
  if (trimmed.includes('.') || trimmed.includes(',')) {
    return 'Points must be a whole number.';
  }
  if (!/^\d+$/.test(trimmed)) {
    return 'Please enter a valid whole number.';
  }
  const val = Number(trimmed);
  if (isNaN(val) || val < 0) {
    return 'Points must be non-negative.';
  }
  if (!Number.isInteger(val)) {
    return 'Points must be a whole number.';
  }
  if (val > 2147483647) {
    return 'Points cannot exceed 2,147,483,647.';
  }
  return null;
}

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
  const initialPoints = initialData?.points ?? 1000;
  const isPresetPoint = PRESET_POINTS.includes(initialPoints as (typeof PRESET_POINTS)[number]);

  const [text, setText] = useState<string>(() => initialData?.text ?? '');
  const [imageUrl, setImageUrl] = useState<string | null>(() => initialData?.imageUrl ?? null);
  const [timeLimitSeconds, setTimeLimitSeconds] = useState<number>(
    () => initialData?.timeLimitSeconds ?? 20,
  );
  const [pointsMode, setPointsMode] = useState<string>(() =>
    isPresetPoint ? String(initialPoints) : 'custom',
  );
  const [customPointsInput, setCustomPointsInput] = useState<string>(() => String(initialPoints));

  const customPointsError =
    pointsMode === 'custom' ? validateCustomPoints(customPointsInput) : null;

  const handlePointsModeChange = (mode: string) => {
    setPointsMode(mode);
    if (validationError) setValidationError(null);
  };

  const handleCustomPointsChange = (val: string) => {
    setCustomPointsInput(val);
    if (validationError) setValidationError(null);
  };
  const [choices, setChoices] = useState<DraftChoice[]>(() =>
    initialData
      ? initialData.choices.map((c, i) => ({
          draftId: c.id || `init-${i}`,
          id: c.id,
          text: c.text,
          isCorrect: c.isCorrect,
        }))
      : DEFAULT_CHOICES.map((c) => ({ ...c, draftId: crypto.randomUUID() })),
  );
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleChoiceChange = (draftId: string, updated: ChoiceInput) => {
    setChoices((prev) => prev.map((c) => (c.draftId === draftId ? { ...c, ...updated } : c)));
    if (validationError) setValidationError(null);
  };

  const handleToggleCorrect = (draftId: string) => {
    setChoices((prev) =>
      prev.map((c) => (c.draftId === draftId ? { ...c, isCorrect: !c.isCorrect } : c)),
    );
    if (validationError) setValidationError(null);
  };

  const handleAddChoice = () => {
    if (choices.length >= 6) return;
    setChoices((prev) => [
      ...prev,
      { draftId: crypto.randomUUID(), id: null, text: '', isCorrect: false },
    ]);
  };

  const handleRemoveChoice = (draftId: string) => {
    if (choices.length <= 2) return;
    setChoices((prev) => {
      const next = prev.filter((c) => c.draftId !== draftId);
      if (!next.some((c) => c.isCorrect) && next.length > 0) {
        return next.map((c, i) => (i === 0 ? { ...c, isCorrect: true } : c));
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
      setValidationError('At least one choice must be marked as correct.');
      return;
    }

    const invalidChoice = choices.some((c) => !c.text || !c.text.trim());
    if (invalidChoice) {
      setValidationError('Every choice must have answer text.');
      return;
    }

    let finalPoints: number;
    if (pointsMode === 'custom') {
      const customErr = validateCustomPoints(customPointsInput);
      if (customErr) {
        setValidationError(customErr);
        return;
      }
      finalPoints = Number(customPointsInput.trim());
    } else {
      finalPoints = Number(pointsMode);
    }

    setValidationError(null);

    await onSubmit({
      text: text.trim(),
      imageUrl,
      timeLimitSeconds,
      points: finalPoints,
      choices: choices.map((choice) => ({
        id: choice.id,
        text: choice.text,
        isCorrect: choice.isCorrect,
      })),
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
                <Stack spacing={1.5}>
                  <FormControl fullWidth size="small">
                    <InputLabel id="points-select-label">Points</InputLabel>
                    <Select
                      labelId="points-select-label"
                      value={pointsMode}
                      label="Points"
                      onChange={(e) => handlePointsModeChange(e.target.value)}
                      disabled={isSaving}
                    >
                      <MenuItem value="1000">Standard (1000 pts)</MenuItem>
                      <MenuItem value="2000">Double Points (2000 pts)</MenuItem>
                      <MenuItem value="0">No Points (0 pts)</MenuItem>
                      <MenuItem value="custom">Custom Points</MenuItem>
                    </Select>
                  </FormControl>

                  {pointsMode === 'custom' && (
                    <TextField
                      id="custom-points-input"
                      label="Custom Points"
                      fullWidth
                      size="small"
                      value={customPointsInput}
                      onChange={(e) => handleCustomPointsChange(e.target.value)}
                      disabled={isSaving}
                      error={Boolean(customPointsError)}
                      helperText={customPointsError ?? 'Enter non-negative whole number'}
                      slotProps={{
                        input: {
                          endAdornment: <InputAdornment position="end">pts</InputAdornment>,
                          inputProps: { min: 0, step: 1, 'aria-label': 'Custom Points' },
                        },
                      }}
                    />
                  )}
                </Stack>
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
                  <Grid key={choice.draftId} size={{ xs: 12, md: 6 }}>
                    <ChoiceEditorRow
                      index={index}
                      choice={choice}
                      onChange={(updated) => handleChoiceChange(choice.draftId, updated)}
                      onRemove={() => handleRemoveChoice(choice.draftId)}
                      onToggleCorrect={() => handleToggleCorrect(choice.draftId)}
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
            disabled={isSaving || (pointsMode === 'custom' && Boolean(customPointsError))}
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
