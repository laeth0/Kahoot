import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import HelpOutlinedIcon from '@mui/icons-material/HelpOutlined';
import ImageIcon from '@mui/icons-material/Image';
import TimerOutlinedIcon from '@mui/icons-material/TimerOutlined';
import {
  Box,
  Card,
  CardContent,
  Chip,
  IconButton,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';

import type { QuestionResponse } from '../../api/quizService.ts';

export interface ReorderableQuestionListProps {
  questions: QuestionResponse[];
  onEdit: (question: QuestionResponse) => void;
  onDelete: (questionId: string) => void;
  onMoveUp: (index: number) => void;
  onMoveDown: (index: number) => void;
  isReordering?: boolean;
}

export function ReorderableQuestionList({
  questions,
  onEdit,
  onDelete,
  onMoveUp,
  onMoveDown,
  isReordering = false,
}: ReorderableQuestionListProps) {
  if (questions.length === 0) {
    return (
      <Paper
        elevation={0}
        sx={{
          p: 6,
          textAlign: 'center',
          bgcolor: '#ffffff',
          borderRadius: 3,
          border: '2px dashed #d9e2ec',
        }}
      >
        <Box
          sx={{
            width: 64,
            height: 64,
            borderRadius: '50%',
            bgcolor: 'rgba(0, 98, 155, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            mx: 'auto',
            mb: 2,
            color: '#00629b',
          }}
        >
          <HelpOutlinedIcon sx={{ fontSize: 36 }} />
        </Box>
        <Typography variant="h6" sx={{ fontWeight: 800, color: '#09131f', mb: 1 }}>
          No questions added yet
        </Typography>
        <Typography variant="body2" sx={{ color: '#486581', maxWidth: 460, mx: 'auto' }}>
          This quiz doesn't have any questions yet. Add questions with at least 2 choices and 1
          correct answer to make it ready for players.
        </Typography>
      </Paper>
    );
  }

  const sortedQuestions = [...questions].sort((a, b) => a.orderIndex - b.orderIndex);

  return (
    <Stack spacing={2}>
      {sortedQuestions.map((q, index) => {
        const isFirst = index === 0;
        const isLast = index === sortedQuestions.length - 1;
        const correctChoice = q.choices.find((c) => c.isCorrect);

        return (
          <Card
            key={q.id}
            variant="outlined"
            sx={{
              borderRadius: 2.5,
              borderColor: '#e2e8f0',
              bgcolor: '#ffffff',
              transition: 'all 0.2s ease-in-out',
              '&:hover': {
                borderColor: '#0284c7',
                boxShadow: '0 4px 16px rgba(0, 98, 155, 0.08)',
              },
            }}
          >
            <CardContent sx={{ p: { xs: 2, sm: 2.5 }, '&:last-child': { pb: { xs: 2, sm: 2.5 } } }}>
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={2}
                sx={{ alignItems: { sm: 'flex-start' }, justifyContent: 'space-between' }}
              >
                <Stack direction="row" spacing={2} sx={{ flex: 1, minWidth: 0 }}>
                  <Box
                    sx={{
                      width: 42,
                      height: 42,
                      borderRadius: 2,
                      bgcolor: '#00629b',
                      color: '#ffffff',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontWeight: 800,
                      fontSize: '1.1rem',
                      flexShrink: 0,
                    }}
                  >
                    #{index + 1}
                  </Box>

                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Stack
                      direction="row"
                      spacing={1}
                      sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1, mb: 1 }}
                    >
                      <Chip
                        icon={<TimerOutlinedIcon sx={{ fontSize: 16 }} />}
                        label={`${q.timeLimitSeconds}s`}
                        size="small"
                        sx={{
                          bgcolor: '#f0f4f8',
                          color: '#334e68',
                          fontWeight: 700,
                          fontSize: '0.75rem',
                        }}
                      />
                      <Chip
                        label={`${q.points} pts`}
                        size="small"
                        sx={{
                          bgcolor: '#e0f2fe',
                          color: '#0284c7',
                          fontWeight: 700,
                          fontSize: '0.75rem',
                        }}
                      />
                      <Chip
                        label={`${q.choices.length} choices`}
                        size="small"
                        sx={{
                          bgcolor: '#f8fafc',
                          color: '#627d98',
                          fontWeight: 600,
                          fontSize: '0.75rem',
                        }}
                      />
                      {q.imageUrl && (
                        <Chip
                          icon={<ImageIcon sx={{ fontSize: 16 }} />}
                          label="Image attached"
                          size="small"
                          sx={{
                            bgcolor: '#f1f5f9',
                            color: '#475569',
                            fontWeight: 600,
                            fontSize: '0.75rem',
                          }}
                        />
                      )}
                    </Stack>

                    <Typography
                      variant="body1"
                      sx={{
                        fontWeight: 700,
                        color: '#09131f',
                        lineHeight: 1.4,
                        mb: 1.5,
                        wordBreak: 'break-word',
                      }}
                    >
                      {q.text}
                    </Typography>

                    {correctChoice && (
                      <Stack
                        direction="row"
                        spacing={1}
                        sx={{
                          alignItems: 'center',
                          bgcolor: 'rgba(16, 185, 129, 0.08)',
                          p: 1,
                          borderRadius: 1.5,
                          border: '1px solid rgba(16, 185, 129, 0.2)',
                          maxWidth: 540,
                        }}
                      >
                        <CheckCircleOutlinedIcon sx={{ color: '#10b981', fontSize: 18 }} />
                        <Typography
                          variant="caption"
                          sx={{
                            fontWeight: 700,
                            color: '#065f46',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                        >
                          Correct: {correctChoice.text || '(Image answer)'}
                        </Typography>
                      </Stack>
                    )}
                  </Box>
                </Stack>

                <Stack
                  direction="row"
                  spacing={0.5}
                  sx={{
                    alignItems: 'center',
                    justifyContent: { xs: 'flex-end', sm: 'flex-start' },
                    alignSelf: { xs: 'flex-end', sm: 'center' },
                    pt: { xs: 1, sm: 0 },
                    borderTop: { xs: '1px solid #f1f5f9', sm: 'none' },
                    width: { xs: '100%', sm: 'auto' },
                  }}
                >
                  <Tooltip title="Move question up">
                    <span>
                      <IconButton
                        size="small"
                        onClick={() => onMoveUp(index)}
                        disabled={isFirst || isReordering}
                        aria-label={`Move question ${index + 1} up`}
                        sx={{ color: '#486581' }}
                      >
                        <ArrowUpwardIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>

                  <Tooltip title="Move question down">
                    <span>
                      <IconButton
                        size="small"
                        onClick={() => onMoveDown(index)}
                        disabled={isLast || isReordering}
                        aria-label={`Move question ${index + 1} down`}
                        sx={{ color: '#486581' }}
                      >
                        <ArrowDownwardIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>

                  <Tooltip title="Edit question">
                    <IconButton
                      size="small"
                      onClick={() => onEdit(q)}
                      aria-label={`Edit question ${index + 1}`}
                      sx={{ color: '#00629b' }}
                    >
                      <EditOutlinedIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>

                  <Tooltip title="Delete question">
                    <IconButton
                      size="small"
                      onClick={() => onDelete(q.id)}
                      aria-label={`Delete question ${index + 1}`}
                      sx={{ color: '#ef4444' }}
                    >
                      <DeleteOutlinedIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        );
      })}
    </Stack>
  );
}

export default ReorderableQuestionList;
