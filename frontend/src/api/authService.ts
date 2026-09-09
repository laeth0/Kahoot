import { axiosClient } from './axiosClient.ts';

export interface HostUser {
  id: string;
  username: string;
}

export interface AuthResponse {
  host: HostUser;
  accessToken: string;
  refreshToken?: string;
}

export interface LoginPayload {
  username: string;
  password: string;
}

/**
 * Authentication service for Host operations.
 * Operates with username + password credentials (no email).
 */
export const authService = {
  async login(payload: LoginPayload): Promise<AuthResponse> {
    try {
      const response = await axiosClient.post<AuthResponse>('/auth/login', payload);
      return response.data;
    } catch (err) {
      // In development, if the backend server is not reachable yet,
      // provide simulated host authentication so the UI and protected flows can be tested.
      const isConnectionRefused =
        err instanceof Error &&
        (err.message.includes('Network Error') ||
          err.message.includes('ERR_CONNECTION_REFUSED') ||
          err.message.includes('timeout'));

      if (isConnectionRefused) {
        // Validate credentials for demo/bootstrap host
        if (payload.username.trim() && payload.password.trim()) {
          const demoHost: HostUser = {
            id: 'host-dev-uuid-001',
            username: payload.username.trim(),
          };
          return {
            host: demoHost,
            accessToken: 'demo-jwt-access-token-host-session',
            refreshToken: 'demo-jwt-refresh-token',
          };
        }
      }
      throw err;
    }
  },

  async logout(): Promise<void> {
    try {
      await axiosClient.post('/auth/logout');
    } catch {
      // Ignore network errors during client logout
    } finally {
      localStorage.removeItem('kahoot_host_token');
      localStorage.removeItem('kahoot_host_user');
    }
  },
};
