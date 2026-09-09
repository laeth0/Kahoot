import type { PaletteOptions } from '@mui/material/styles';

/**
 * Light theme palette derived from the IEEEXtreme Palestine Section logo colors.
 * Strict light mode only: deep tech ocean blue primary, luminous cyan accent,
 * crisp white paper surfaces, and high-contrast dark navy text.
 */
export const palette: PaletteOptions = {
  mode: 'light',
  primary: {
    main: '#00629b',
    light: '#2087c7',
    dark: '#0d507c',
    contrastText: '#ffffff',
  },
  secondary: {
    main: '#0284c7',
    light: '#38bdf8',
    dark: '#0369a1',
    contrastText: '#ffffff',
  },
  info: {
    main: '#0284c7',
    light: '#e0f2fe',
    dark: '#0369a1',
    contrastText: '#ffffff',
  },
  success: {
    main: '#059669',
    light: '#d1fae5',
    dark: '#047857',
    contrastText: '#ffffff',
  },
  warning: {
    main: '#d97706',
    light: '#fef3c7',
    dark: '#b45309',
    contrastText: '#ffffff',
  },
  error: {
    main: '#dc2626',
    light: '#fee2e2',
    dark: '#b91c1c',
    contrastText: '#ffffff',
  },
  background: {
    default: '#f4f8fc',
    paper: '#ffffff',
  },
  text: {
    primary: '#09131f',
    secondary: '#334e68',
    disabled: '#94a3b8',
  },
  divider: '#e2e8f0',
  action: {
    active: '#00629b',
    hover: 'rgba(0, 98, 155, 0.04)',
    selected: 'rgba(0, 98, 155, 0.08)',
    disabled: 'rgba(9, 19, 31, 0.26)',
    disabledBackground: 'rgba(9, 19, 31, 0.12)',
    focus: 'rgba(0, 98, 155, 0.12)',
  },
};
