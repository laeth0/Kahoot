import { Box } from '@mui/material';

import { resolveMediaUrl } from '../../api/media.ts';

export interface QuestionMediaProps {
  imageUrl?: string | null;
  alt?: string;
  maxHeight?: number;
}

export function QuestionMedia({ imageUrl, alt = '', maxHeight = 320 }: QuestionMediaProps) {
  const src = resolveMediaUrl(imageUrl);
  if (!src) {
    return null;
  }

  return (
    <Box
      sx={{
        width: '100%',
        display: 'flex',
        justifyContent: 'center',
      }}
    >
      <Box
        component="img"
        src={src}
        alt={alt}
        loading="lazy"
        sx={{
          width: 'auto',
          maxWidth: '100%',
          maxHeight,
          aspectRatio: '16 / 9',
          objectFit: 'contain',
          borderRadius: 3,
          border: '1px solid #E2E8F0',
          bgcolor: '#FFFFFF',
        }}
      />
    </Box>
  );
}

export default QuestionMedia;
