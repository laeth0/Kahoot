import { axiosClient, configureAuthInterceptor } from './axiosClient.ts';

export interface HostUser {
  id: string;
  username: string;
}

export interface AuthResponse {
  host: HostUser;
  accessToken: string;
  accessTokenExpiresAt: string;
}

export interface LoginPayload {
  username: string;
  password: string;
}

interface HostAuthResponseDto {
  hostId: string;
  username: string;
  accessToken: string;
  accessTokenExpiresAt: string;
}

const LEGACY_STORAGE_KEYS = [
  'kahoot_host_token',
  'kahoot_host_user',
  'kahoot_host_refresh_token',
  'kahoot_host_access_expires_at',
  'kahoot_host_refresh_expires_at',
];

if (typeof window !== 'undefined') {
  for (const key of LEGACY_STORAGE_KEYS) {
    try {
      localStorage.removeItem(key);
    } catch {
      // Ignore storage access restrictions
    }
  }
}

let inMemoryAccessToken: string | null = null;
let inMemoryHost: HostUser | null = null;
let inMemoryAccessTokenExpiresAt: string | null = null;

export const authChannel =
  typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('kahoot_auth_channel') : null;

function setSessionState(dto: HostAuthResponseDto | null): AuthResponse | null {
  if (!dto) {
    inMemoryAccessToken = null;
    inMemoryHost = null;
    inMemoryAccessTokenExpiresAt = null;
    return null;
  }

  inMemoryAccessToken = dto.accessToken;
  inMemoryHost = { id: dto.hostId, username: dto.username };
  inMemoryAccessTokenExpiresAt = dto.accessTokenExpiresAt;

  return {
    host: inMemoryHost,
    accessToken: inMemoryAccessToken,
    accessTokenExpiresAt: inMemoryAccessTokenExpiresAt,
  };
}

export const authService = {
  async login(payload: LoginPayload): Promise<AuthResponse> {
    const response = await axiosClient.post<HostAuthResponseDto>('/auth/login', payload, {
      withCredentials: true,
    });
    const auth = setSessionState(response.data)!;
    authChannel?.postMessage({ type: 'AUTH_LOGIN', host: auth.host });
    return auth;
  },

  async refresh(): Promise<AuthResponse> {
    const response = await axiosClient.post<HostAuthResponseDto>(
      '/auth/refresh',
      {},
      {
        withCredentials: true,
        headers: {
          'X-Requested-With': 'XMLHttpRequest',
        },
      },
    );
    const auth = setSessionState(response.data)!;
    authChannel?.postMessage({ type: 'AUTH_REFRESH', host: auth.host });
    return auth;
  },

  async bootstrap(): Promise<AuthResponse | null> {
    try {
      return await this.refresh();
    } catch {
      setSessionState(null);
      return null;
    }
  },

  getAccessToken(): string | null {
    return inMemoryAccessToken;
  },

  getHost(): HostUser | null {
    return inMemoryHost;
  },

  getAccessTokenExpiresAt(): string | null {
    return inMemoryAccessTokenExpiresAt;
  },

  async logout(): Promise<void> {
    try {
      await axiosClient.post(
        '/auth/logout',
        {},
        {
          withCredentials: true,
          headers: {
            'X-Requested-With': 'XMLHttpRequest',
          },
        },
      );
    } catch {
      // Ignore network failures on logout
    } finally {
      setSessionState(null);
      authChannel?.postMessage({ type: 'AUTH_LOGOUT' });
    }
  },

  clearSession(): void {
    setSessionState(null);
  },
};

configureAuthInterceptor(
  () => authService.getAccessToken(),
  async () => {
    try {
      const response = await authService.refresh();
      return response.accessToken;
    } catch {
      return null;
    }
  },
);
