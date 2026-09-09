import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import QuizOutlinedIcon from '@mui/icons-material/QuizOutlined';
import {
  Button,
  Card,
  CardActions,
  CardContent,
  Chip,
  CircularProgress,
  IconButton,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';

import type { QuizSummaryResponse } from '../../api/quizService.ts';

export interface QuizCardProps {
  quiz: QuizSummaryResponse;
  onEdit: (quizId: string) => void;
  onStartGame: (quizId: string) => void;
  onDelete: (quiz: QuizSummaryResponse) => void;
  isStartingGame?: boolean;
}

export function QuizCard({
  quiz,
  onEdit,
  onStartGame,
  onDelete,
  isStartingGame = false,
}: QuizCardProps) {
  const canStartGame = quiz.isPublished && quiz.questionCount > 0;

  const getStartGameTooltip = () => {
    if (!quiz.isPublished) {
      return 'Publish this quiz in the editor before hosting a live game.';
    }
    if (quiz.questionCount === 0) {
      return 'Add at least one question before hosting a live game.';
    }
    return 'Launch a new live game session for players to join.';
  };

  return (
    <Card
      variant="outlined"
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        borderRadius: 3,
        borderColor: '#e2e8f0',
        bgcolor: '#ffffff',
        transition: 'all 0.25s cubic-bezier(0.4, 0, 0.2, 1)',
        '&:hover': {
          transform: 'translateY(-4px)',
          borderColor: '#0284c7',
          boxShadow: '0 12px 28px rgba(0, 98, 155, 0.12)',
        },
      }}
    >
      <CardContent sx={{ p: 3, flexGrow: 1 }}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ mb: 2, alignItems: 'center', justifyContent: 'space-between' }}
        >
          <Chip
            size="small"
            label={quiz.isPublished ? 'Published' : 'Draft'}
            sx={{
              fontWeight: 700,
              fontSize: '0.75rem',
              bgcolor: quiz.isPublished ? 'rgba(16, 185, 129, 0.12)' : 'rgba(245, 158, 11, 0.12)',
              color: quiz.isPublished ? '#065f46' : '#92400e',
              border: '1px solid',
              borderColor: quiz.isPublished ? 'rgba(16, 185, 129, 0.3)' : 'rgba(245, 158, 11, 0.3)',
            }}
          />

          <Chip
            icon={<QuizOutlinedIcon sx={{ fontSize: 16 }} />}
            size="small"
            label={`${quiz.questionCount} ${quiz.questionCount === 1 ? 'question' : 'questions'}`}
            sx={{
              fontWeight: 600,
              fontSize: '0.75rem',
              bgcolor: '#f0f4f8',
              color: '#334e68',
            }}
          />
        </Stack>

        <Typography
          variant="h6"
          component="h2"
          sx={{
            fontWeight: 800,
            color: '#09131f',
            mb: 1,
            lineHeight: 1.3,
            overflow: 'hidden',
            display: '-webkit-box',
            WebkitLineClamp: 2,
            WebkitBoxOrient: 'vertical',
            minHeight: '2.6em',
          }}
        >
          {quiz.title}
        </Typography>

        <Typography
          variant="body2"
          sx={{
            color: '#486581',
            lineHeight: 1.5,
            overflow: 'hidden',
            display: '-webkit-box',
            WebkitLineClamp: 3,
            WebkitBoxOrient: 'vertical',
            minHeight: '4.5em',
          }}
        >
          {quiz.description || 'No description provided.'}
        </Typography>
      </CardContent>

      <CardActions
        sx={{
          p: 2.5,
          pt: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          borderTop: '1px solid #f1f5f9',
        }}
      >
        <Stack direction="row" spacing={1}>
          <Tooltip title={getStartGameTooltip()}>
            <span>
              <Button
                variant="contained"
                color="primary"
                size="small"
                startIcon={
                  isStartingGame ? (
                    <CircularProgress size={16} color="inherit" />
                  ) : (
                    <PlayArrowIcon />
                  )
                }
                onClick={() => onStartGame(quiz.id)}
                disabled={!canStartGame || isStartingGame}
                sx={{
                  fontWeight: 700,
                  borderRadius: 2,
                  px: 2,
                  textTransform: 'none',
                }}
              >
                Host Game
              </Button>
            </span>
          </Tooltip>

          <Button
            variant="outlined"
            size="small"
            startIcon={<EditOutlinedIcon />}
            onClick={() => onEdit(quiz.id)}
            sx={{
              fontWeight: 700,
              borderRadius: 2,
              borderColor: '#cbd5e1',
              color: '#00629b',
              textTransform: 'none',
              '&:hover': {
                borderColor: '#00629b',
                bgcolor: 'rgba(0, 98, 155, 0.04)',
              },
            }}
          >
            Edit
          </Button>
        </Stack>

        <Tooltip title="Delete quiz">
          <IconButton
            size="small"
            aria-label={`Delete ${quiz.title}`}
            onClick={() => onDelete(quiz)}
            sx={{
              color: '#94a3b8',
              '&:hover': {
                color: '#ef4444',
                bgcolor: 'rgba(239, 68, 68, 0.08)',
              },
            }}
          >
            <DeleteOutlinedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </CardActions>
    </Card>
  );
}

export default QuizCard;
