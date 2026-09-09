import {
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from '@mui/material';
import type { ReactNode } from 'react';

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  content: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmColor?: 'primary' | 'error' | 'secondary';
  isConfirming?: boolean;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
}

export function ConfirmDialog({
  open,
  title,
  content,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  confirmColor = 'primary',
  isConfirming = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={isConfirming ? undefined : onCancel}
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-description"
      PaperProps={{
        sx: {
          borderRadius: 3,
          p: 1,
          maxWidth: 440,
          width: '100%',
        },
      }}
    >
      <DialogTitle id="confirm-dialog-title" sx={{ fontWeight: 700, color: '#09131f' }}>
        {title}
      </DialogTitle>
      <DialogContent>
        {typeof content === 'string' ? (
          <DialogContentText id="confirm-dialog-description" sx={{ color: '#334e68' }}>
            {content}
          </DialogContentText>
        ) : (
          content
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2, gap: 1 }}>
        <Button
          onClick={onCancel}
          disabled={isConfirming}
          variant="outlined"
          color="inherit"
          sx={{ minHeight: 40, fontWeight: 600 }}
        >
          {cancelLabel}
        </Button>
        <Button
          onClick={onConfirm}
          disabled={isConfirming}
          variant="contained"
          color={confirmColor}
          startIcon={isConfirming ? <CircularProgress size={18} color="inherit" /> : null}
          sx={{ minHeight: 40, fontWeight: 700, px: 2.5 }}
        >
          {isConfirming ? 'Processing...' : confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default ConfirmDialog;
