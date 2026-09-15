import { type ReactNode, useCallback, useEffect, useState } from 'react';

import { authChannel, authService, type HostUser } from '../api/authService.ts';
import { AuthContext } from './AuthContext.ts';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [host, setHost] = useState<HostUser | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [accessTokenExpiresAt, setAccessTokenExpiresAt] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    authService
      .bootstrap()
      .then((auth) => {
        if (isMounted && auth) {
          setHost(auth.host);
          setToken(auth.accessToken);
          setAccessTokenExpiresAt(auth.accessTokenExpiresAt);
        }
      })
      .finally(() => {
        if (isMounted) {
          setIsLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    const channel = authChannel;
    if (!channel) {
      return;
    }

    const handleMessage = (event: MessageEvent) => {
      const data = event.data;
      if (data?.type === 'AUTH_LOGOUT') {
        setHost(null);
        setToken(null);
        setAccessTokenExpiresAt(null);
      } else if (data?.type === 'AUTH_LOGIN' || data?.type === 'AUTH_REFRESH') {
        setHost(data.host ?? authService.getHost());
        setToken(authService.getAccessToken());
        setAccessTokenExpiresAt(authService.getAccessTokenExpiresAt());
      }
    };

    channel.addEventListener('message', handleMessage);
    return () => {
      channel.removeEventListener('message', handleMessage);
    };
  }, []);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  const logout = useCallback(async () => {
    setIsLoading(true);
    try {
      await authService.logout();
    } finally {
      setHost(null);
      setToken(null);
      setAccessTokenExpiresAt(null);
      setError(null);
      setIsLoading(false);
    }
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await authService.login({ username, password });
      setHost(response.host);
      setToken(response.accessToken);
      setAccessTokenExpiresAt(response.accessTokenExpiresAt);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Invalid host credentials';
      setError(msg);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const refreshSession = useCallback(async (): Promise<boolean> => {
    try {
      const response = await authService.refresh();
      setHost(response.host);
      setToken(response.accessToken);
      setAccessTokenExpiresAt(response.accessTokenExpiresAt);
      return true;
    } catch (error: unknown) {
      const status =
        (error as { status?: number })?.status ??
        (error as { response?: { status?: number } })?.response?.status;
      if (status === 401 || status === 403) {
        await logout();
      }
      return false;
    }
  }, [logout]);

  const value = {
    host,
    token,
    refreshToken: null,
    accessTokenExpiresAt,
    refreshTokenExpiresAt: null,
    isAuthenticated: Boolean(token && host),
    isLoading,
    error,
    login,
    logout,
    refreshSession,
    clearError,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export default AuthProvider;
