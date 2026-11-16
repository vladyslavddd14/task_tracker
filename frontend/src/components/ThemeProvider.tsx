import { createContext, useContext, type ReactNode } from 'react';
import { useTheme, type Theme } from '../hooks/hooks';

const ThemeContext = createContext<{ theme: Theme; toggle: () => void } | null>(null);

/** Applies the light/dark theme to the page and shares the toggle with the header button. */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, toggle] = useTheme();
  return <ThemeContext.Provider value={{ theme, toggle }}>{children}</ThemeContext.Provider>;
}

export function useThemeToggle() {
  const context = useContext(ThemeContext);
  if (!context) throw new Error('useThemeToggle must be used inside ThemeProvider');
  return context;
}
