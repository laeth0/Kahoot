import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import ImageNotSupportedOutlinedIcon from '@mui/icons-material/ImageNotSupportedOutlined';
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
import { type ChangeEvent, useEffect, useRef, useState } from 'react';

import { isApiRequestCanceled } from '../../api/axiosClient.ts';
import { resolveMediaUrl } from '../../api/media.ts';
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
  const uploadControllerRef = useRef<AbortController | null>(null);
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [failedValue, setFailedValue] = useState<string | null>(null);

  const previewSrc = resolveMediaUrl(value) ?? value;
  const previewError = Boolean(value && failedValue === value);

  useEffect(() => {
    return () => {
      const controller = uploadControllerRef.current;
      uploadControllerRef.current = null;
      controller?.abort();
    };
  }, []);

  const handleButtonClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    uploadControllerRef.current?.abort();
    const controller = new AbortController();
    uploadControllerRef.current = controller;
    setError(null);
    setIsUploading(true);

    try {
      const result = await uploadService.uploadImage(file, controller.signal);
      onChange(result.url);
    } catch (err) {
      if (!isApiRequestCanceled(err)) {
        const msg = err instanceof Error ? err.message : 'Image upload failed';
        setError(msg);
      }
    } finally {
      if (uploadControllerRef.current === controller) {
        uploadControllerRef.current = null;
        setIsUploading(false);
        if (fileInputRef.current) {
          fileInputRef.current.value = '';
        }
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
            {previewError ? (
              <Box
                sx={{
                  width: 64,
                  height: 64,
                  borderRadius: 1.5,
                  border: '1px solid #cbd5e1',
                  bgcolor: '#ffffff',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#94a3b8',
                }}
              >
                <ImageNotSupportedOutlinedIcon />
              </Box>
            ) : (
              <Box
                component="img"
                src={previewSrc ?? undefined}
                alt="Uploaded preview"
                onError={() => setFailedValue(value ?? null)}
                sx={{
                  width: 64,
                  height: 64,
                  borderRadius: 1.5,
                  objectFit: 'cover',
                  border: '1px solid #cbd5e1',
                  bgcolor: '#ffffff',
                }}
              />
            )}
            <Typography
              variant="body2"
              noWrap
              sx={{ color: '#334e68', maxWidth: { xs: 160, sm: 260 }, fontWeight: 500 }}
            >
              {previewError ? 'Image attached (preview unavailable)' : 'Image attached'}
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
