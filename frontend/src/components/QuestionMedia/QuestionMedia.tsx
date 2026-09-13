import ImageNotSupportedOutlinedIcon from '@mui/icons-material/ImageNotSupportedOutlined';
import { Box, Typography } from '@mui/material';
import { useState } from 'react';

import { resolveMediaUrl } from '../../api/media.ts';

export interface QuestionMediaProps {
  imageUrl?: string | null;
  alt?: string;
}

export function QuestionMedia({ imageUrl, alt = '' }: QuestionMediaProps) {
  const [failedSrc, setFailedSrc] = useState<string | null>(null);
  const src = resolveMediaUrl(imageUrl);

  if (!src) {
    return null;
  }

  if (failedSrc === src) {
    return (
      <Box
        sx={{
          width: '100%',
          display: 'flex',
          justifyContent: 'center',
          my: 1,
        }}
      >
        <Box
          sx={{
            width: '100%',
            maxWidth: 600,
            height: 'clamp(140px, 25vh, 260px)',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 1,
            borderRadius: 3,
            border: '1px dashed #CBD5E1',
            bgcolor: '#F8FAFC',
            color: '#64748B',
          }}
        >
          <ImageNotSupportedOutlinedIcon sx={{ fontSize: 36, color: '#94A3B8' }} />
          <Typography variant="body2" sx={{ fontWeight: 500, color: '#64748B' }}>
            Image could not be loaded
          </Typography>
        </Box>
      </Box>
    );
  }

  return (
    <Box
      sx={{
        width: '100%',
        display: 'flex',
        justifyContent: 'center',
        my: 1,
      }}
    >
      <Box
        component="img"
        src={src}
        alt={alt}
        onError={() => setFailedSrc(src)}
        loading="lazy"
        sx={{
          width: 'auto',
          maxWidth: '100%',
          maxHeight: 'clamp(160px, 35vh, 420px)',
          objectFit: 'contain',
          borderRadius: 3,
          border: '1px solid #E2E8F0',
          bgcolor: '#FFFFFF',
          boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
        }}
      />
    </Box>
  );
}

export default QuestionMedia;
