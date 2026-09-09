import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { Box, Button, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

export interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: ReactNode;
  actionLabel?: string;
  onAction?: () => void;
  actionIcon?: ReactNode;
}

export function EmptyState({
  title,
  description,
  icon,
  actionLabel,
  onAction,
  actionIcon,
}: EmptyStateProps) {
  return (
    <Paper
      elevation={0}
      sx={{
        p: { xs: 4, sm: 6 },
        textAlign: 'center',
        borderRadius: 3,
        border: '1px dashed #cbd5e1',
        bgcolor: '#ffffff',
      }}
    >
      <Stack spacing={2} sx={{ alignItems: 'center', maxWidth: 420, mx: 'auto' }}>
        <Box
          sx={{
            width: 64,
            height: 64,
            borderRadius: '50%',
            bgcolor: '#eef7fc',
            color: '#00629b',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          {icon ?? <InboxOutlinedIcon sx={{ fontSize: 32 }} />}
        </Box>

        <Typography variant="h6" component="h3" sx={{ fontWeight: 700, color: '#09131f' }}>
          {title}
        </Typography>

        {description && (
          <Typography variant="body2" sx={{ color: 'text.secondary', lineHeight: 1.6 }}>
            {description}
          </Typography>
        )}

        {actionLabel && onAction && (
          <Button
            variant="contained"
            color="primary"
            onClick={onAction}
            startIcon={actionIcon}
            sx={{ minHeight: 44, px: 3, mt: 1, fontWeight: 600 }}
          >
            {actionLabel}
          </Button>
        )}
      </Stack>
    </Paper>
  );
}

export default EmptyState;
