import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { onSessionExpired, refreshSession, setAccessToken, signIn, signOut, type Schemas } from './api';

type User = Schemas['UserInfo'];

interface AuthState {
  user: User | null;
  isAdmin: boolean;
  ready: boolean;
  login: (email: string, password: string) => Promise<'ok' | 'invalid' | 'locked' | 'disabled' | 'offline'>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<User | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    refreshSession()
      .then((session) => setUser(session?.user ?? null))
      .finally(() => setReady(true));
  }, []);

  useEffect(() => {
    onSessionExpired(() => {
      setAccessToken(null);
      setUser(null);
      queryClient.clear();
    });
    return () => onSessionExpired(null);
  }, [queryClient]);

  const login = useCallback<AuthState['login']>(async (email, password) => {
    try {
      const result = await signIn(email, password);
      if (!result.ok) return result.status === 423 ? 'locked' : result.status === 403 ? 'disabled' : 'invalid';
      queryClient.clear();
      setUser(result.body.user);
      return 'ok';
    } catch {
      return 'offline';
    }
  }, [queryClient]);

  const logout = useCallback(async () => {
    await signOut();
    setUser(null);
    queryClient.clear();
  }, [queryClient]);

  const value = useMemo(() => ({ user, isAdmin: user?.role === 'Admin', ready, login, logout }), [user, ready, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}
