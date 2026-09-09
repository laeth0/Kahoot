import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { type ChangeEvent, useRef, useState } from 'react';

import { uploadService } from '../../api/uploadService.ts';

export interface ImageUploadFieldProps {
  value?: string | null;
  onChange: (url: string | null) => void;
  label?: string;
  disabled?: boolean;
}

export function ImageUploadField({
  value,
  onChange,
  label = 'Question Image',
  disabled = false,
}: ImageUploadFieldProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const handleButtonClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    setError(null);
    setIsUploading(true);

    try {
      const result = await uploadService.uploadImage(file);
      onChange(result.url);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Image upload failed';
      setError(msg);
    } finally {
      setIsUploading(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const handleRemove = () => {
    onChange(null);
    setError(null);
  };

  return (
    <Box sx={{ width: '100%' }}>
      <Typography
        component="span"
        variant="subtitle2"
        sx={{ fontWeight: 600, color: '#09131f', mb: 1, display: 'block' }}
      >
        {label}
      </Typography>

      <input
        ref={fileInputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp,image/gif"
        onChange={handleFileChange}
        style={{ display: 'none' }}
        disabled={disabled || isUploading}
        aria-hidden="true"
      />

      {error && (
        <Alert severity="error" role="alert" sx={{ mb: 1.5 }}>
          {error}
        </Alert>
      )}

      {value ? (
        <Paper
          elevation={0}
          sx={{
            p: 1.5,
            borderRadius: 2,
            border: '1px solid #e2e8f0',
            bgcolor: '#f8fafc',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <Stack direction="row" spacing={2} sx={{ alignItems: 'center', minWidth: 0 }}>
            <Box
              component="img"
              src={value}
              alt="Uploaded preview"
              sx={{
                width: 64,
                height: 64,
                borderRadius: 1.5,
                objectFit: 'cover',
                border: '1px solid #cbd5e1',
                bgcolor: '#ffffff',
              }}
            />
            <Typography
              variant="body2"
              noWrap
              sx={{ color: '#334e68', maxWidth: { xs: 160, sm: 260 }, fontWeight: 500 }}
            >
              Image attached
            </Typography>
          </Stack>

          <IconButton
            aria-label="Remove uploaded image"
            color="error"
            onClick={handleRemove}
            disabled={disabled || isUploading}
            size="small"
          >
            <DeleteOutlinedIcon />
          </IconButton>
        </Paper>
      ) : (
        <Button
          variant="outlined"
          color="primary"
          onClick={handleButtonClick}
          disabled={disabled || isUploading}
          startIcon={
            isUploading ? <CircularProgress size={18} color="inherit" /> : <CloudUploadIcon />
          }
          fullWidth
          sx={{
            minHeight: 48,
            borderStyle: 'dashed',
            borderWidth: '1.5px',
            bgcolor: '#ffffff',
            fontWeight: 600,
            textTransform: 'none',
          }}
        >
          {isUploading ? 'Uploading image...' : 'Upload Question Image (Max 5 MB)'}
        </Button>
      )}
    </Box>
  );
}

export default ImageUploadField;
