import { axiosClient } from './axiosClient.ts';

export interface HostUser {
  id: string;
  username: string;
}

export interface AuthResponse {
  host: HostUser;
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

export interface LoginPayload {
  username: string;
  password: string;
}

interface AuthenticationResponseDto {
  hostId: string;
  username: string;
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

function toAuthResponse(dto: AuthenticationResponseDto): AuthResponse {
  return {
    host: { id: dto.hostId, username: dto.username },
    accessToken: dto.accessToken,
    accessTokenExpiresAt: dto.accessTokenExpiresAt,
    refreshToken: dto.refreshToken,
    refreshTokenExpiresAt: dto.refreshTokenExpiresAt,
  };
}

export const authService = {
  async login(payload: LoginPayload): Promise<AuthResponse> {
    const response = await axiosClient.post<AuthenticationResponseDto>('/auth/login', payload);
    return toAuthResponse(response.data);
  },

  async refresh(refreshToken: string): Promise<AuthResponse> {
    const response = await axiosClient.post<AuthenticationResponseDto>('/auth/refresh', {
      refreshToken,
    });
    return toAuthResponse(response.data);
  },

  async logout(refreshToken?: string | null): Promise<void> {
    try {
      if (refreshToken) {
        await axiosClient.post('/auth/logout', { refreshToken });
      }
    } catch (err) {
      void err;
    }
  },
};
