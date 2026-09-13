import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import HelpOutlinedIcon from '@mui/icons-material/HelpOutlined';
import { Box, Card, IconButton, Paper, Portal, Stack, Tooltip, Typography } from '@mui/material';
import { useId, useMemo } from 'react';

import type { QuestionResponse } from '../../api/quizService.ts';
import { LiveRegion } from '../Feedback/index.ts';
import { QuestionCardContent } from './QuestionCardContent.tsx';
import { useQuestionDrag } from './useQuestionDrag.ts';

export interface ReorderableQuestionListProps {
  questions: QuestionResponse[];
  onEdit: (question: QuestionResponse) => void;
  onDelete: (questionId: string) => void;
  onReorder: (orderedQuestionIds: string[]) => void;
  isReordering?: boolean;
}

export function ReorderableQuestionList({
  questions,
  onEdit,
  onDelete,
  onReorder,
  isReordering = false,
}: ReorderableQuestionListProps) {
  const instructionsId = useId();
  const sortedQuestions = useMemo(
    () => [...questions].sort((a, b) => a.orderIndex - b.orderIndex),
    [questions],
  );
  const questionIds = useMemo(
    () => sortedQuestions.map((question) => question.id),
    [sortedQuestions],
  );
  const { listRef, previewRef, drag, destination, announcement, onPointerDown, onKeyDown } =
    useQuestionDrag({
      questionIds,
      disabled: isReordering,
      onReorder,
    });
  const draggedQuestion = drag
    ? sortedQuestions.find((question) => question.id === drag.questionId)
    : null;
  const dropMessage = destination
    ? destination.beforeIndex === null
      ? 'Drop here to move to the end'
      : `Drop here to move before Question #${destination.beforeIndex + 1}`
    : '';
  const controlsDisabled = isReordering || drag !== null;

  function moveQuestion(index: number, destinationIndex: number) {
    if (controlsDisabled || destinationIndex < 0 || destinationIndex >= questionIds.length) return;
    const orderedIds = [...questionIds];
    const [questionId] = orderedIds.splice(index, 1);
    orderedIds.splice(destinationIndex, 0, questionId);
    onReorder(orderedIds);
  }

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

  return (
    <Box>
      <Typography
        id={instructionsId}
        variant="caption"
        color="text.secondary"
        sx={{ display: 'block', mb: 2 }}
      >
        Drag the six-dot handle to reorder. For keyboard, press Space or Enter, use arrow keys, then
        press again to drop. Escape cancels. You can also use the up/down buttons.
      </Typography>
      <LiveRegion message={dropMessage || announcement} />
      <Box
        ref={listRef}
        role="list"
        aria-label="Quiz questions"
        aria-busy={isReordering}
        sx={{ position: 'relative', overflowAnchor: 'none' }}
      >
        <Stack spacing={2}>
          {sortedQuestions.map((question, index) => (
            <Card
              key={question.id}
              data-question-id={question.id}
              role="listitem"
              aria-label={`Question ${index + 1}`}
              variant="outlined"
              sx={{
                borderRadius: 2.5,
                borderColor: 'divider',
                bgcolor: 'background.paper',
                opacity: drag?.questionId === question.id ? 0.25 : 1,
                transition: 'border-color 0.2s, box-shadow 0.2s',
                '@media (prefers-reduced-motion: reduce)': { transition: 'none' },
                '&:hover': { borderColor: 'primary.main', boxShadow: 2 },
              }}
            >
              <QuestionCardContent
                question={question}
                position={index + 1}
                dragHandle={
                  <Tooltip title="Drag to reorder question">
                    <span>
                      <IconButton
                        aria-label={`Drag question ${index + 1}`}
                        aria-describedby={instructionsId}
                        aria-pressed={drag?.questionId === question.id}
                        disabled={questionIds.length < 2}
                        aria-disabled={isReordering || questionIds.length < 2}
                        onPointerDown={(event) => onPointerDown(event, question.id)}
                        onKeyDown={(event) => onKeyDown(event, question.id)}
                        sx={{
                          width: 44,
                          height: 44,
                          color: 'text.secondary',
                          cursor: 'grab',
                          touchAction: 'none',
                          '&:active': { cursor: 'grabbing' },
                        }}
                      >
                        <DragIndicatorIcon />
                      </IconButton>
                    </span>
                  </Tooltip>
                }
                actions={
                  <Stack
                    direction="row"
                    spacing={0.5}
                    sx={{
                      alignItems: 'center',
                      justifyContent: 'flex-end',
                      alignSelf: { xs: 'flex-end', sm: 'center' },
                      flexWrap: 'wrap',
                    }}
                  >
                    <Tooltip title="Move question up">
                      <span>
                        <IconButton
                          onClick={() => moveQuestion(index, index - 1)}
                          disabled={index === 0 || controlsDisabled}
                          aria-label={`Move question ${index + 1} up`}
                          sx={{ width: 44, height: 44, color: 'text.secondary' }}
                        >
                          <ArrowUpwardIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title="Move question down">
                      <span>
                        <IconButton
                          onClick={() => moveQuestion(index, index + 1)}
                          disabled={index === questionIds.length - 1 || controlsDisabled}
                          aria-label={`Move question ${index + 1} down`}
                          sx={{ width: 44, height: 44, color: 'text.secondary' }}
                        >
                          <ArrowDownwardIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title="Edit question">
                      <span>
                        <IconButton
                          onClick={() => onEdit(question)}
                          disabled={controlsDisabled}
                          aria-label={`Edit question ${index + 1}`}
                          sx={{ width: 44, height: 44, color: 'primary.main' }}
                        >
                          <EditOutlinedIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title="Delete question">
                      <span>
                        <IconButton
                          onClick={() => onDelete(question.id)}
                          disabled={controlsDisabled}
                          aria-label={`Delete question ${index + 1}`}
                          sx={{ width: 44, height: 44, color: 'error.main' }}
                        >
                          <DeleteOutlinedIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </Stack>
                }
              />
            </Card>
          ))}
        </Stack>
        {destination && (
          <Paper
            elevation={0}
            sx={{
              position: 'absolute',
              top: destination.top,
              left: 0,
              right: 0,
              transform: 'translateY(-50%)',
              zIndex: (theme) => theme.zIndex.tooltip + 2,
              pointerEvents: 'none',
              border: '2px dashed',
              borderColor: 'primary.main',
              bgcolor: 'info.light',
              color: 'primary.main',
              borderRadius: 2,
              minHeight: 56,
              px: 2,
              py: 1,
              display: 'flex',
              alignItems: 'center',
              gap: 1.5,
            }}
          >
            <ArrowForwardIcon />
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              {dropMessage}
            </Typography>
          </Paper>
        )}
      </Box>
      {drag && draggedQuestion && (
        <Portal>
          <Box
            ref={previewRef}
            aria-hidden="true"
            sx={{
              position: 'fixed',
              top: 0,
              left: 0,
              width: drag.width,
              maxWidth: 'calc(100vw - 24px)',
              visibility: 'hidden',
              zIndex: (theme) => theme.zIndex.tooltip + 1,
              pointerEvents: 'none',
            }}
          >
            <Card
              sx={{
                borderRadius: 2.5,
                border: '2px solid',
                borderColor: 'primary.main',
                boxShadow: 16,
                transform: 'rotate(-1.5deg)',
                maxHeight: 'calc(100vh - 24px)',
                overflow: 'hidden',
                '@media (prefers-reduced-motion: reduce)': { transform: 'none' },
              }}
            >
              <QuestionCardContent
                question={draggedQuestion}
                position={drag.sourceIndex + 1}
                dragHandle={
                  <DragIndicatorIcon sx={{ color: 'primary.main', flexShrink: 0, mt: 1 }} />
                }
              />
            </Card>
          </Box>
        </Portal>
      )}
    </Box>
  );
}

export default ReorderableQuestionList;
