import { MD3LightTheme, type MD3Theme } from 'react-native-paper';

export const theme: MD3Theme = {
  ...MD3LightTheme,
  colors: {
    ...MD3LightTheme.colors,
    primary: '#1F3A5F',
    onPrimary: '#FFFFFF',
    primaryContainer: '#DDEBF7',
    onPrimaryContainer: '#0D1F33',
    secondary: '#F5A623',
    background: '#F5F7FA',
  },
};
