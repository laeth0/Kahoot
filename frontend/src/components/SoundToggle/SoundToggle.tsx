import VolumeOffIcon from '@mui/icons-material/VolumeOff';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import { IconButton, Tooltip } from '@mui/material';
import React, { useEffect, useState } from 'react';

import { isMuted, subscribeMuteChange, toggleMuted } from '../../utils/celebrationAudio';

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
  const [muted, setMutedState] = useState<boolean>(() => isMuted());

  useEffect(() => {
    return subscribeMuteChange((next) => {
      setMutedState(next);
    });
  }, []);

  const handleToggle = () => {
    toggleMuted();
  };

  const label = muted ? 'Unmute celebration sound' : 'Mute celebration sound';

  return (
    <Tooltip title={label} arrow>
      <IconButton
        onClick={handleToggle}
        aria-label={label}
        size={size}
        color={color}
        className={className}
        sx={{
          bgcolor: 'rgba(255, 255, 255, 0.9)',
          border: '1px solid rgba(0, 98, 155, 0.15)',
          color: muted ? 'text.secondary' : '#00629B',
          '&:hover': {
            bgcolor: 'rgba(244, 248, 252, 1)',
            borderColor: '#00629B',
          },
        }}
      >
        {muted ? <VolumeOffIcon fontSize={size} /> : <VolumeUpIcon fontSize={size} />}
      </IconButton>
    </Tooltip>
  );
};
