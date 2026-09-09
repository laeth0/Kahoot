import { createTheme } from '@mui/material/styles';

import { components } from './components.ts';
import { palette } from './palette.ts';
import { typography } from './typography.ts';

export const theme = createTheme({
  palette,
  typography,
  components,
  shape: {
    borderRadius: 8,
  },
});

export default theme;
