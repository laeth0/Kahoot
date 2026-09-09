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
  content?: ReactNode;
  message?: string;
  children?: ReactNode;
  confirmLabel?: string;
  confirmText?: string;
  cancelLabel?: string;
  cancelText?: string;
  confirmColor?: 'primary' | 'error' | 'secondary';
  severity?: string;
  isConfirming?: boolean;
  isLoading?: boolean;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
}

export function ConfirmDialog({
  open,
  title,
  content,
  message,
  children,
  confirmLabel,
  confirmText,
  cancelLabel,
  cancelText,
  confirmColor,
  severity,
  isConfirming,
  isLoading,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const finalConfirmLabel = confirmLabel || confirmText || 'Confirm';
  const finalCancelLabel = cancelLabel || cancelText || 'Cancel';
  const finalIsLoading = Boolean(isConfirming || isLoading);
  const finalConfirmColor: 'primary' | 'error' | 'secondary' =
    confirmColor || (severity === 'error' ? 'error' : 'primary');
  const finalContent = content ?? message;

  return (
    <Dialog
      open={open}
      onClose={finalIsLoading ? undefined : onCancel}
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-description"
      slotProps={{
        paper: {
          sx: {
            borderRadius: 3,
            p: 1,
            maxWidth: 440,
            width: '100%',
          },
        },
      }}
    >
      <DialogTitle id="confirm-dialog-title" sx={{ fontWeight: 700, color: '#09131f' }}>
        {title}
      </DialogTitle>
      <DialogContent>
        {typeof finalContent === 'string' ? (
          <DialogContentText id="confirm-dialog-description" sx={{ color: '#334e68' }}>
            {finalContent}
          </DialogContentText>
        ) : (
          finalContent
        )}
        {children}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2, gap: 1 }}>
        <Button
          onClick={onCancel}
          disabled={finalIsLoading}
          variant="outlined"
          color="inherit"
          sx={{ minHeight: 40, fontWeight: 600 }}
        >
          {finalCancelLabel}
        </Button>
        <Button
          onClick={onConfirm}
          disabled={finalIsLoading}
          variant="contained"
          color={finalConfirmColor}
          startIcon={finalIsLoading ? <CircularProgress size={18} color="inherit" /> : null}
          sx={{ minHeight: 40, fontWeight: 700, px: 2.5 }}
        >
          {finalIsLoading ? 'Processing...' : finalConfirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default ConfirmDialog;
