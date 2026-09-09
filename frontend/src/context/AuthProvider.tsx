import { type ReactNode, useCallback, useEffect, useState } from 'react';

import { authService, type HostUser } from '../api/authService.ts';
import {
  ACCESS_TOKEN_EXPIRES_KEY,
  REFRESH_TOKEN_EXPIRES_KEY,
  REFRESH_TOKEN_STORAGE_KEY,
  TOKEN_STORAGE_KEY,
  USER_STORAGE_KEY,
} from '../api/axiosClient.ts';
import { AuthContext } from './AuthContext.ts';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [host, setHost] = useState<HostUser | null>(() => {
    const savedHost = localStorage.getItem(USER_STORAGE_KEY);
    if (savedHost) {
      try {
        return JSON.parse(savedHost) as HostUser;
      } catch {
        return null;
      }
    }
    return null;
  });

  const [token, setToken] = useState<string | null>(() => {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  });

  const [refreshToken, setRefreshToken] = useState<string | null>(() => {
    return localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
  });

  const [accessTokenExpiresAt, setAccessTokenExpiresAt] = useState<string | null>(() => {
    return localStorage.getItem(ACCESS_TOKEN_EXPIRES_KEY);
  });

  const [refreshTokenExpiresAt, setRefreshTokenExpiresAt] = useState<string | null>(() => {
    return localStorage.getItem(REFRESH_TOKEN_EXPIRES_KEY);
  });

  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (token && host && refreshToken) {
      localStorage.setItem(TOKEN_STORAGE_KEY, token);
      localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(host));
      localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, refreshToken);
      if (accessTokenExpiresAt) {
        localStorage.setItem(ACCESS_TOKEN_EXPIRES_KEY, accessTokenExpiresAt);
      }
      if (refreshTokenExpiresAt) {
        localStorage.setItem(REFRESH_TOKEN_EXPIRES_KEY, refreshTokenExpiresAt);
      }
    } else if (!token || !refreshToken) {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      localStorage.removeItem(USER_STORAGE_KEY);
      localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
      localStorage.removeItem(ACCESS_TOKEN_EXPIRES_KEY);
      localStorage.removeItem(REFRESH_TOKEN_EXPIRES_KEY);
    }
  }, [token, host, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt]);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  const logout = useCallback(async () => {
    setIsLoading(true);
    try {
      if (refreshToken) {
        await authService.logout(refreshToken);
      }
    } finally {
      setHost(null);
      setToken(null);
      setRefreshToken(null);
      setAccessTokenExpiresAt(null);
      setRefreshTokenExpiresAt(null);
      setError(null);
      setIsLoading(false);
    }
  }, [refreshToken]);

  const login = useCallback(async (username: string, password: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await authService.login({ username, password });
      setHost(response.host);
      setToken(response.accessToken);
      setRefreshToken(response.refreshToken);
      setAccessTokenExpiresAt(response.accessTokenExpiresAt);
      setRefreshTokenExpiresAt(response.refreshTokenExpiresAt);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Invalid host credentials';
      setError(msg);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const refreshSession = useCallback(async (): Promise<boolean> => {
    const currentRefreshToken = refreshToken ?? localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
    if (!currentRefreshToken) {
      return false;
    }

    try {
      const response = await authService.refresh(currentRefreshToken);
      setHost(response.host);
      setToken(response.accessToken);
      setRefreshToken(response.refreshToken);
      setAccessTokenExpiresAt(response.accessTokenExpiresAt);
      setRefreshTokenExpiresAt(response.refreshTokenExpiresAt);
      return true;
    } catch {
      await logout();
      return false;
    }
  }, [refreshToken, logout]);

  const value = {
    host,
    token,
    refreshToken,
    accessTokenExpiresAt,
    refreshTokenExpiresAt,
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
