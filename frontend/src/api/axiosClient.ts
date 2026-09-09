import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api';

export const TOKEN_STORAGE_KEY = 'kahoot_host_token';
export const USER_STORAGE_KEY = 'kahoot_host_user';
export const REFRESH_TOKEN_STORAGE_KEY = 'kahoot_host_refresh_token';
export const ACCESS_TOKEN_EXPIRES_KEY = 'kahoot_host_access_expires_at';
export const REFRESH_TOKEN_EXPIRES_KEY = 'kahoot_host_refresh_expires_at';

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

export const axiosClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 10000,
});

axiosClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem(TOKEN_STORAGE_KEY);
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
  (error: AxiosError<ProblemDetails>) => {
    const status = error.response?.status;

    if (status === 401) {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      localStorage.removeItem(USER_STORAGE_KEY);
      localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
      localStorage.removeItem(ACCESS_TOKEN_EXPIRES_KEY);
      localStorage.removeItem(REFRESH_TOKEN_EXPIRES_KEY);
    }

    const data = error.response?.data;

    if (data?.errors && typeof data.errors === 'object') {
      const firstKey = Object.keys(data.errors)[0];
      const firstError = firstKey && data.errors[firstKey]?.[0];
      if (firstError) {
        return Promise.reject(new ApiError(firstError, data.code, status));
      }
    }

    if (status === 429) {
      return Promise.reject(
        new ApiError(
          'Too many requests. Please wait a moment before trying again.',
          data?.code ?? 'Http.RateLimited',
          429,
        ),
      );
    }

    const message =
      data?.detail ??
      data?.title ??
      data?.message ??
      data?.error ??
      error.message ??
      'An unexpected network error occurred';

    return Promise.reject(new ApiError(message, data?.code, status));
  },
);

export default axiosClient;
