import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { onSessionExpired, refreshSession, setAccessToken, type UserInfo } from '../api/client';
import { applyLanguage, type Language } from '../i18n';
import type { Schemas } from '../api/client';

interface AuthState {
  user: UserInfo | null;
  isAdmin: boolean;
  initializing: boolean;
  sessionExpired: boolean;
  login: (email: string, password: string) => Promise<'ok' | 'invalid' | 'locked' | 'disabled'>;
  logout: () => Promise<void>;
  updateUser: (user: UserInfo) => void;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<UserInfo | null>(null);
  const [initializing, setInitializing] = useState(true);
  const [sessionExpired, setSessionExpired] = useState(false);

  const adopt = useCallback((u: UserInfo) => {
    setUser(u);
    setSessionExpired(false);
    if (u.preferredLanguage === 'ar' || u.preferredLanguage === 'en') applyLanguage(u.preferredLanguage as Language);
  }, []);

  useEffect(() => {
    let cancelled = false;
    refreshSession()
      .then((result) => {
        if (!cancelled && result) adopt(result.user);
      })
      .finally(() => !cancelled && setInitializing(false));
    return () => {
      cancelled = true;
    };
  }, [adopt]);

  useEffect(() => {
    onSessionExpired(() => {
      setAccessToken(null);
      setUser(null);
      setSessionExpired(true);
      queryClient.clear();
    });
    return () => onSessionExpired(null);
  }, [queryClient]);

  const login = useCallback<AuthState['login']>(
    async (email, password) => {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify({ email, password, client: 'Web' }),
      });
      if (response.status === 423) return 'locked';
      if (response.status === 403) return 'disabled';
      if (!response.ok) return 'invalid';
      const body = (await response.json()) as Schemas['AuthResponse'];
      setAccessToken(body.accessToken);
      queryClient.clear();
      adopt(body.user);
      return 'ok';
    },
    [adopt, queryClient],
  );

  const logout = useCallback(async () => {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' }).catch(() => undefined);
    setAccessToken(null);
    setUser(null);
    queryClient.clear();
  }, [queryClient]);

  const value = useMemo<AuthState>(
    () => ({ user, isAdmin: user?.role === 'Admin', initializing, sessionExpired, login, logout, updateUser: setUser }),
    [user, initializing, sessionExpired, login, logout],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}
