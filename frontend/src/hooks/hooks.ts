import { useCallback, useEffect, useState } from 'react';

/** useState that is mirrored to localStorage. Works even when storage is unavailable. */
export function useLocalStorage<T extends string>(
  key: string,
  initial: T,
  allowed?: readonly T[],
): [T, (value: T) => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const stored = window.localStorage.getItem(key) as T | null;
      if (stored && (!allowed || allowed.includes(stored))) {
        return stored;
      }
    } catch {
      // Ignore storage errors and use the default.
    }
    return initial;
  });

  const update = useCallback(
    (next: T) => {
      setValue(next);
      try {
        window.localStorage.setItem(key, next);
      } catch {
        // The choice just will not be remembered.
      }
    },
    [key],
  );

  return [value, update];
}

export function useDebounce<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

export type ViewMode = 'list' | 'board' | 'calendar';
export const VIEW_MODES: readonly ViewMode[] = ['list', 'board', 'calendar'];

/** The selected tasks view (list, kanban or calendar) is remembered in the browser. */
export function useViewMode(): [ViewMode, (mode: ViewMode) => void] {
  return useLocalStorage<ViewMode>('tt.view', 'list', VIEW_MODES);
}

export type Theme = 'light' | 'dark';

/** Light or dark theme. Without a stored choice the system preference is used. */
export function useTheme(): [Theme, () => void] {
  const [stored, setStored] = useLocalStorage<Theme | 'system'>('tt.theme', 'system', [
    'light',
    'dark',
    'system',
  ]);
  const systemDark =
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches;
  const theme: Theme = stored === 'system' ? (systemDark ? 'dark' : 'light') : stored;

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  const toggle = useCallback(
    () => setStored(theme === 'dark' ? 'light' : 'dark'),
    [theme, setStored],
  );
  return [theme, toggle];
}
