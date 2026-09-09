import CancelIcon from '@mui/icons-material/Cancel';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { Box, ButtonBase, Chip, Stack, Typography } from '@mui/material';

import { resolveMediaUrl } from '../../api/media.ts';
import { ChoiceShape } from './ChoiceShape.tsx';
import { choiceColor, choiceLetter } from './choiceVisuals.ts';

export type ChoiceReveal = 'correct' | 'incorrect' | 'muted' | null;

export interface ChoiceButtonProps {
  index: number;
  text?: string | null;
  imageUrl?: string | null;
  onClick?: () => void;
  selected?: boolean;
  disabled?: boolean;
  reveal?: ChoiceReveal;
  count?: number | null;
}

export function ChoiceButton({
  index,
  text,
  imageUrl,
  onClick,
  selected = false,
  disabled = false,
  reveal = null,
  count = null,
}: ChoiceButtonProps) {
  const letter = choiceLetter(index);
  const baseColor = choiceColor(index);
  const media = resolveMediaUrl(imageUrl);

  const muted = reveal === 'muted';
  const outlineColor =
    reveal === 'correct'
      ? '#10B981'
      : reveal === 'incorrect'
        ? '#EF4444'
        : selected
          ? '#FFFFFF'
          : 'transparent';

  const label = text?.trim() ? text : `Answer ${letter}`;

  return (
    <ButtonBase
      onClick={onClick}
      disabled={disabled || !onClick}
      aria-pressed={onClick ? selected : undefined}
      focusRipple
      sx={{
        width: '100%',
        minHeight: { xs: 76, sm: 92 },
        borderRadius: 3,
        px: 2,
        py: 1.5,
        color: '#FFFFFF',
        bgcolor: baseColor,
        opacity: muted ? 0.4 : 1,
        filter: muted ? 'grayscale(0.6)' : 'none',
        boxShadow: selected
          ? '0 8px 20px rgba(9, 19, 31, 0.25)'
          : '0 2px 6px rgba(9, 19, 31, 0.12)',
        border: '4px solid',
        borderColor: outlineColor,
        transform: selected && !reveal ? 'translateY(-2px)' : 'none',
        transition: 'transform 0.15s ease, opacity 0.15s ease, border-color 0.15s ease',
        justifyContent: 'flex-start',
        textAlign: 'left',
        '&.Mui-disabled': { color: '#FFFFFF' },
        '@media (prefers-reduced-motion: reduce)': { transition: 'none' },
      }}
    >
      <Stack
        direction="row"
        spacing={1.5}
        sx={{ alignItems: 'center', width: '100%', minWidth: 0 }}
      >
        <Stack
          sx={{
            alignItems: 'center',
            justifyContent: 'center',
            width: 40,
            height: 40,
            borderRadius: 2,
            bgcolor: 'rgba(255, 255, 255, 0.18)',
            flexShrink: 0,
          }}
        >
          <ChoiceShape index={index} fontSize={20} />
          <Typography sx={{ fontWeight: 900, fontSize: '0.7rem', lineHeight: 1 }}>
            {letter}
          </Typography>
        </Stack>

        {media ? (
          <Box
            component="img"
            src={media}
            alt={label}
            loading="lazy"
            sx={{ height: 56, maxWidth: '60%', objectFit: 'contain', borderRadius: 1 }}
          />
        ) : (
          <Typography
            sx={{
              fontWeight: 800,
              fontSize: { xs: '1rem', sm: '1.1rem' },
              lineHeight: 1.3,
              overflowWrap: 'anywhere',
            }}
          >
            {label}
          </Typography>
        )}

        <Box sx={{ flexGrow: 1 }} />

        {reveal === 'correct' && <CheckCircleIcon sx={{ fontSize: 26, flexShrink: 0 }} />}
        {reveal === 'incorrect' && <CancelIcon sx={{ fontSize: 26, flexShrink: 0 }} />}

        {count !== null && (
          <Chip
            label={count}
            size="small"
            sx={{
              bgcolor: 'rgba(255, 255, 255, 0.9)',
              color: '#09131F',
              fontWeight: 800,
              flexShrink: 0,
            }}
          />
        )}
      </Stack>
    </ButtonBase>
  );
}

export default ChoiceButton;
