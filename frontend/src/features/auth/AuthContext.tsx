import { useQueryClient } from '@tanstack/react-query';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api } from '../../api/endpoints';
import { setUnauthorizedHandler, tokenStore } from '../../api/client';
import type { AuthResponse, ProfileTemplate, User } from '../../api/types';

type Status = 'loading' | 'authenticated' | 'anonymous';

interface RegisterInput {
  email: string;
  password: string;
  displayName: string;
  timeZoneId: string;
  profileTemplate: ProfileTemplate;
}

interface AuthContextValue {
  user: User | null;
  status: Status;
  login: (email: string, password: string) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => void;
  setUser: (user: User) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<User | null>(null);
  const [status, setStatus] = useState<Status>(() => (tokenStore.get() ? 'loading' : 'anonymous'));

  const signOut = useCallback(() => {
    tokenStore.clear();
    queryClient.clear();
    setUser(null);
    setStatus('anonymous');
  }, [queryClient]);

  // A 401 from any protected request ends the session and sends the user to the login page.
  useEffect(() => {
    setUnauthorizedHandler(signOut);
    return () => setUnauthorizedHandler(null);
  }, [signOut]);

  // Restore the session after a page reload.
  useEffect(() => {
    if (!tokenStore.get()) return;
    api.me
      .get()
      .then((me) => {
        setUser(me);
        setStatus('authenticated');
      })
      .catch(() => signOut());
  }, [signOut]);

  const completeSignIn = useCallback(
    (response: AuthResponse) => {
      queryClient.clear(); // never show cached data of the previous account
      tokenStore.set(response.accessToken);
      setUser(response.user);
      setStatus('authenticated');
    },
    [queryClient],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      status,
      login: async (email, password) => completeSignIn(await api.auth.login(email, password)),
      register: async (input) => completeSignIn(await api.auth.register(input)),
      logout: signOut,
      setUser,
    }),
    [user, status, completeSignIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside AuthProvider');
  return context;
}

/** The signed-in user. Only use it on pages behind RequireAuth. */
export function useCurrentUser(): User {
  const { user } = useAuth();
  if (!user) throw new Error('No signed-in user');
  return user;
}
