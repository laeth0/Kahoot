import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL ?? '/api';

export class ApiError extends Error {
  readonly code?: string;
  readonly status?: number;

  constructor(message: string, code?: string, status?: number) {
    super(message);
    this.name = 'ApiError';
    this.code = code;
    this.status = status;
  }
}

export function isApiRequestCanceled(error: unknown): boolean {
  return axios.isCancel(error);
}

let tokenGetter: (() => string | null) | null = null;
let refreshHandler: (() => Promise<string | null>) | null = null;

export function configureAuthInterceptor(
  getToken: () => string | null,
  refreshToken: () => Promise<string | null>,
): void {
  tokenGetter = getToken;
  refreshHandler = refreshToken;
}

export const axiosClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 10000,
});

axiosClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = tokenGetter?.();
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error: AxiosError) => Promise.reject(error),
);

interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  message?: string;
  error?: string;
  code?: string;
}

axiosClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    if (axios.isCancel(error)) {
      return Promise.reject(error);
    }

    const status = error.response?.status;
    const data = error.response?.data;
    const originalRequest = error.config as
      (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;

    if (status === 401 && originalRequest) {
      const requestUrl = originalRequest.url ?? '';
      const isAuthEndpoint =
        requestUrl.includes('/auth/login') ||
        requestUrl.includes('/auth/refresh') ||
        requestUrl.includes('/auth/logout');

      if (isAuthEndpoint || originalRequest._retry) {
        if (
          !isAuthEndpoint &&
          typeof window !== 'undefined' &&
          window.location.pathname !== '/login'
        ) {
          sessionStorage.setItem('kahoot_session_expired', 'true');
          const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
          window.location.assign(`/login?returnUrl=${returnUrl}`);
        }
        return Promise.reject(
          new ApiError(
            data?.detail ?? data?.message ?? 'Your session has expired. Please sign in again.',
            data?.code ?? 'Auth.Unauthorized',
            401,
          ),
        );
      }

      originalRequest._retry = true;

      if (!refreshHandler) {
        return Promise.reject(
          new ApiError('Your session has expired. Please sign in again.', 'Auth.Unauthorized', 401),
        );
      }

      try {
        const newToken = await refreshHandler();
        if (!newToken) {
          throw new Error('Session refresh failed');
        }

        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${newToken}`;
        }
        return await axiosClient(originalRequest);
      } catch {
        if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
          sessionStorage.setItem('kahoot_session_expired', 'true');
          const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
          window.location.assign(`/login?returnUrl=${returnUrl}`);
        }
        return Promise.reject(
          new ApiError('Your session has expired. Please sign in again.', 'Auth.Unauthorized', 401),
        );
      }
    }

    if (status === 403) {
      return Promise.reject(
        new ApiError(
          data?.detail ?? data?.message ?? 'You do not have permission to perform this action.',
          data?.code ?? 'Auth.Forbidden',
          403,
        ),
      );
    }

    if (data?.errors && typeof data.errors === 'object') {
      const firstKey = Object.keys(data.errors)[0];
      const firstError = firstKey && data.errors[firstKey]?.[0];
      if (firstError) {
        return Promise.reject(new ApiError(firstError, data.code, status));
      }
    }

    if (status === 404) {
      return Promise.reject(
        new ApiError(
          data?.detail ?? data?.message ?? 'The requested resource was not found.',
          data?.code ?? 'Http.NotFound',
          404,
        ),
      );
    }

    if (status === 409) {
      return Promise.reject(
        new ApiError(
          data?.detail ?? data?.message ?? 'A conflict occurred with the current game state.',
          data?.code ?? 'Game.Conflict',
          409,
        ),
      );
    }

    if (status === 429) {
      return Promise.reject(
        new ApiError(
          data?.detail ?? 'Too many requests. Please wait a moment before trying again.',
          data?.code ?? 'Http.RateLimited',
          429,
        ),
      );
    }

    if (status && status >= 500) {
      return Promise.reject(
        new ApiError(
          data?.detail ?? 'A server error occurred. Please try again.',
          data?.code ?? 'Server.Error',
          status,
        ),
      );
    }

    const message =
      data?.detail ??
      data?.title ??
      data?.message ??
      data?.error ??
      error.message ??
      'An unexpected error occurred. Please try again.';

    return Promise.reject(new ApiError(message, data?.code, status));
  },
);
