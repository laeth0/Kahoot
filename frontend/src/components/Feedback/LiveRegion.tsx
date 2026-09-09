import { Box } from '@mui/material';

export interface LiveRegionProps {
  message?: string | null;
  politeness?: 'polite' | 'assertive';
}

export function LiveRegion({ message, politeness = 'polite' }: LiveRegionProps) {
  return (
    <Box
      role="status"
      aria-live={politeness}
      aria-atomic="true"
      sx={{
        position: 'absolute',
        width: 1,
        height: 1,
        padding: 0,
        margin: -1,
        overflow: 'hidden',
        clip: 'rect(0, 0, 0, 0)',
        whiteSpace: 'nowrap',
        border: 0,
      }}
    >
      {message ?? ''}
    </Box>
  );
}

export default LiveRegion;
