import { createTheme } from '@mui/material/styles';
import createCache from '@emotion/cache';
import { prefixer } from 'stylis';
import rtlPlugin from 'stylis-plugin-rtl';

// מטמון Emotion שהופך את כל ה-CSS של MUI לימין-לשמאל.
export const rtlCache = createCache({
  key: 'muirtl',
  stylisPlugins: [prefixer, rtlPlugin],
});

export const theme = createTheme({
  direction: 'rtl',
  typography: {
    fontFamily: '"Rubik", "Segoe UI", Arial, sans-serif',
  },
  palette: {
    primary: { main: '#2f5d8a' },
    background: { default: '#f5f6f8' },
  },
  shape: { borderRadius: 8 },
});
