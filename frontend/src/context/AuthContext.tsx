import {
  createContext,
  type ReactNode,
  useCallback,
  useEffect,
  useState,
} from 'react';

import { authService, type HostUser } from '../api/authService.ts';

export interface AuthContextType {
  host: HostUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  clearError: () => void;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [host, setHost] = useState<HostUser | null>(() => {
    const savedHost = localStorage.getItem('kahoot_host_user');
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
    return localStorage.getItem('kahoot_host_token');
  });

  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (token && host) {
      localStorage.setItem('kahoot_host_token', token);
      localStorage.setItem('kahoot_host_user', JSON.stringify(host));
    } else {
      localStorage.removeItem('kahoot_host_token');
      localStorage.removeItem('kahoot_host_user');
    }
  }, [token, host]);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await authService.login({ username, password });
      setHost(response.host);
      setToken(response.accessToken);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Invalid host credentials';
      setError(msg);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(async () => {
    setIsLoading(true);
    try {
      await authService.logout();
    } finally {
      setHost(null);
      setToken(null);
      setError(null);
      setIsLoading(false);
    }
  }, []);

  const value = {
    host,
    token,
    isAuthenticated: Boolean(token && host),
    isLoading,
    error,
    login,
    logout,
    clearError,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export default AuthProvider;
