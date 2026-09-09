import type { Components, Theme } from '@mui/material/styles';

/**
 * MUI Component overrides tailored to the light theme with IEEE / tech aesthetic:
 * Clean rounded shapes (8px/12px/16px), subtle border lines (#e2e8f0),
 * crisp white elevated surfaces, and refined primary brand accents.
 */
export const components: Components<Theme> = {
  MuiCssBaseline: {
    styleOverrides: {
      html: {
        WebkitFontSmoothing: 'antialiased',
        MozOsxFontSmoothing: 'grayscale',
      },
      body: {
        backgroundColor: '#f4f8fc',
        color: '#09131f',
        scrollbarColor: '#94a3b8 #f4f8fc',
        '&::-webkit-scrollbar': {
          width: 8,
          height: 8,
        },
        '&::-webkit-scrollbar-track': {
          backgroundColor: '#f4f8fc',
        },
        '&::-webkit-scrollbar-thumb': {
          backgroundColor: '#cbd5e1',
          borderRadius: 4,
          '&:hover': {
            backgroundColor: '#94a3b8',
          },
        },
      },
    },
  },
  MuiButton: {
    defaultProps: {
      disableElevation: true,
    },
    styleOverrides: {
      root: {
        borderRadius: 8,
        textTransform: 'none',
        fontWeight: 600,
        padding: '8px 16px',
        transition: 'all 0.15s ease-in-out',
      },
      outlined: {
        borderColor: '#cbd5e1',
        color: '#09131f',
        '&:hover': {
          borderColor: '#00629b',
          backgroundColor: 'rgba(0, 98, 155, 0.04)',
        },
      },
      text: {
        color: '#00629b',
        '&:hover': {
          backgroundColor: 'rgba(0, 98, 155, 0.06)',
        },
      },
    },
    variants: [
      {
        props: { variant: 'contained', color: 'primary' },
        style: {
          backgroundColor: '#00629b',
          color: '#ffffff',
          '&:hover': {
            backgroundColor: '#0d507c',
            boxShadow: '0 4px 6px -1px rgba(0, 98, 155, 0.25)',
          },
        },
      },
      {
        props: { variant: 'contained', color: 'secondary' },
        style: {
          backgroundColor: '#0284c7',
          color: '#ffffff',
          '&:hover': {
            backgroundColor: '#0369a1',
            boxShadow: '0 4px 6px -1px rgba(2, 132, 199, 0.25)',
          },
        },
      },
    ],
  },
  MuiCard: {
    styleOverrides: {
      root: {
        borderRadius: 12,
        backgroundColor: '#ffffff',
        border: '1px solid #e2e8f0',
        boxShadow: '0 1px 3px 0 rgba(9, 19, 31, 0.05), 0 1px 2px -1px rgba(9, 19, 31, 0.03)',
        transition: 'box-shadow 0.2s ease, border-color 0.2s ease',
      },
    },
  },
  MuiPaper: {
    styleOverrides: {
      root: {
        backgroundImage: 'none',
      },
      rounded: {
        borderRadius: 12,
      },
      outlined: {
        borderColor: '#e2e8f0',
      },
    },
  },
  MuiAppBar: {
    defaultProps: {
      elevation: 0,
      color: 'inherit',
    },
    styleOverrides: {
      root: {
        backgroundColor: '#ffffff',
        color: '#09131f',
        borderBottom: '1px solid #e2e8f0',
        boxShadow: '0 1px 2px 0 rgba(9, 19, 31, 0.04)',
      },
    },
  },
  MuiOutlinedInput: {
    styleOverrides: {
      root: {
        borderRadius: 8,
        backgroundColor: '#ffffff',
        '& .MuiOutlinedInput-notchedOutline': {
          borderColor: '#cbd5e1',
          transition: 'border-color 0.15s ease',
        },
        '&:hover .MuiOutlinedInput-notchedOutline': {
          borderColor: '#94a3b8',
        },
        '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
          borderColor: '#00629b',
          borderWidth: 1.5,
        },
      },
      input: {
        padding: '10px 14px',
      },
    },
  },
  MuiChip: {
    styleOverrides: {
      root: {
        borderRadius: 6,
        fontWeight: 500,
      },
    },
    variants: [
      {
        props: { color: 'primary' },
        style: {
          backgroundColor: '#eef7fc',
          color: '#00629b',
        },
      },
      {
        props: { color: 'secondary' },
        style: {
          backgroundColor: '#f0f9ff',
          color: '#0284c7',
        },
      },
    ],
  },
  MuiTableHead: {
    styleOverrides: {
      root: {
        backgroundColor: '#ebf3fa',
        '& .MuiTableCell-head': {
          fontWeight: 600,
          color: '#09131f',
          borderBottom: '1px solid #e2e8f0',
        },
      },
    },
  },
  MuiTableCell: {
    styleOverrides: {
      root: {
        borderColor: '#e2e8f0',
      },
    },
  },
  MuiDialog: {
    styleOverrides: {
      paper: {
        borderRadius: 16,
        border: '1px solid #e2e8f0',
        boxShadow: '0 20px 25px -5px rgba(9, 19, 31, 0.1), 0 8px 10px -6px rgba(9, 19, 31, 0.04)',
      },
    },
  },
  MuiAlert: {
    styleOverrides: {
      root: {
        borderRadius: 8,
      },
    },
    variants: [
      {
        props: { severity: 'success' },
        style: {
          backgroundColor: '#ecfdf5',
          color: '#065f46',
        },
      },
      {
        props: { severity: 'error' },
        style: {
          backgroundColor: '#fef2f2',
          color: '#991b1b',
        },
      },
      {
        props: { severity: 'warning' },
        style: {
          backgroundColor: '#fffbeb',
          color: '#92400e',
        },
      },
      {
        props: { severity: 'info' },
        style: {
          backgroundColor: '#f0f9ff',
          color: '#075985',
        },
      },
    ],
  },
};
