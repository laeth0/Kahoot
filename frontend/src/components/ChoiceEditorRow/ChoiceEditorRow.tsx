import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import {
  Box,
  IconButton,
  Paper,
  Radio,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';

import type { ChoiceInput } from '../../api/quizQuestionService.ts';
import { ImageUploadField } from '../ImageUploadField/ImageUploadField.tsx';

export interface ChoiceEditorRowProps {
  index: number;
  choice: ChoiceInput;
  onChange: (updated: ChoiceInput) => void;
  onRemove: () => void;
  onMarkCorrect: () => void;
  canRemove: boolean;
  disabled?: boolean;
}

const CHOICE_LETTERS = ['A', 'B', 'C', 'D', 'E', 'F'];
const CHOICE_COLORS = ['#e02424', '#0284c7', '#d97706', '#059669', '#7c3aed', '#db2777'];

export function ChoiceEditorRow({
  index,
  choice,
  onChange,
  onRemove,
  onMarkCorrect,
  canRemove,
  disabled = false,
}: ChoiceEditorRowProps) {
  const letter = CHOICE_LETTERS[index] ?? String(index + 1);
  const color = CHOICE_COLORS[index % CHOICE_COLORS.length];

  const handleTextChange = (text: string) => {
    onChange({
      ...choice,
      text: text.slice(0, 300),
    });
  };

  const handleImageChange = (imageUrl: string | null) => {
    onChange({
      ...choice,
      imageUrl,
    });
  };

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2,
        borderRadius: 2.5,
        border: choice.isCorrect ? '2px solid #059669' : '1px solid #e2e8f0',
        bgcolor: choice.isCorrect ? '#f0fdf4' : '#ffffff',
        transition: 'border-color 0.2s ease, background-color 0.2s ease',
      }}
    >
      <Stack spacing={1.5}>
        <Stack
          direction="row"
          spacing={1.5}
          sx={{ alignItems: 'center', justifyContent: 'space-between' }}
        >
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            <Box
              sx={{
                width: 32,
                height: 32,
                borderRadius: 1.5,
                bgcolor: color,
                color: '#ffffff',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontWeight: 800,
                fontSize: '0.9rem',
              }}
            >
              {letter}
            </Box>

            <Tooltip title="Mark as the single correct answer">
              <Stack
                direction="row"
                spacing={0.5}
                onClick={disabled ? undefined : onMarkCorrect}
                sx={{
                  cursor: disabled ? 'default' : 'pointer',
                  alignItems: 'center',
                  userSelect: 'none',
                }}
              >
                <Radio
                  checked={choice.isCorrect}
                  onChange={onMarkCorrect}
                  disabled={disabled}
                  icon={<RadioButtonUncheckedIcon sx={{ fontSize: 20 }} />}
                  checkedIcon={<CheckCircleIcon sx={{ fontSize: 20, color: '#059669' }} />}
                  size="small"
                  slotProps={{
                    input: { 'aria-label': `Mark choice ${letter} as correct` },
                  }}
                />
                <Typography
                  variant="caption"
                  sx={{
                    fontWeight: 700,
                    color: choice.isCorrect ? '#059669' : '#334e68',
                  }}
                >
                  {choice.isCorrect ? 'Correct Answer' : 'Mark Correct'}
                </Typography>
              </Stack>
            </Tooltip>
          </Stack>

          {canRemove && (
            <IconButton
              size="small"
              color="error"
              onClick={onRemove}
              disabled={disabled}
              aria-label={`Delete choice ${letter}`}
            >
              <DeleteOutlinedIcon fontSize="small" />
            </IconButton>
          )}
        </Stack>

        <TextField
          fullWidth
          size="small"
          placeholder={`Enter answer text for choice ${letter}...`}
          value={choice.text ?? ''}
          onChange={(e) => handleTextChange(e.target.value)}
          disabled={disabled}
          slotProps={{
            input: {
              sx: {
                fontWeight: 500,
                bgcolor: '#ffffff',
              },
            },
          }}
          helperText={`${(choice.text ?? '').length}/300 characters`}
        />

        <Box sx={{ pt: 0.5 }}>
          <ImageUploadField
            value={choice.imageUrl}
            onChange={handleImageChange}
            label={`Choice ${letter} Image (optional if text is provided)`}
            disabled={disabled}
          />
        </Box>
      </Stack>
    </Paper>
  );
}

export default ChoiceEditorRow;
