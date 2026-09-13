import VolumeOffIcon from '@mui/icons-material/VolumeOff';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import { IconButton, Tooltip } from '@mui/material';
import React from 'react';

import { useCelebrationAudio } from '../../hooks/useCelebrationAudio.ts';

export interface SoundToggleProps {
  size?: 'small' | 'medium' | 'large';
  color?: 'inherit' | 'primary' | 'default';
  className?: string;
}

export const SoundToggle: React.FC<SoundToggleProps> = ({
  size = 'medium',
  color = 'default',
  className,
}) => {
  const { isMuted, toggleMute } = useCelebrationAudio();

  const label = isMuted ? 'Unmute celebration sound' : 'Mute celebration sound';

  return (
    <Tooltip title={label} arrow>
      <IconButton
        onClick={toggleMute}
        aria-label={label}
        size={size}
        color={color}
        className={className}
        sx={{
          bgcolor: 'rgba(255, 255, 255, 0.9)',
          border: '1px solid rgba(0, 98, 155, 0.15)',
          color: isMuted ? 'text.secondary' : '#00629B',
          '&:hover': {
            bgcolor: 'rgba(244, 248, 252, 1)',
            borderColor: '#00629B',
          },
        }}
      >
        {isMuted ? <VolumeOffIcon fontSize={size} /> : <VolumeUpIcon fontSize={size} />}
      </IconButton>
    </Tooltip>
  );
};

export default SoundToggle;
