import { createTheme } from '@mui/material/styles';

import { components } from './components.ts';
import { palette } from './palette.ts';
import { typography } from './typography.ts';

/**
 * Material UI light theme configured for Kahoot / IEEEXtreme Palestine Section.
 * Strictly light theme only.
 */
export const theme = createTheme({
  palette,
  typography,
  components,
  shape: {
    borderRadius: 8,
  },
});

export default theme;
