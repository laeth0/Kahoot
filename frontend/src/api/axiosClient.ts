import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5048/api';

export const TOKEN_STORAGE_KEY = 'kahoot_host_token';
export const USER_STORAGE_KEY = 'kahoot_host_user';

/**
 * Centralized Axios client: base URL from configuration, bearer-token injection,
 * and ProblemDetails-aware error interpretation.
 */
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
  title?: string;
  detail?: string;
  message?: string;
  error?: string;
}

axiosClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    if (error.response?.status === 401) {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      localStorage.removeItem(USER_STORAGE_KEY);
    }

    const data = error.response?.data;
    const message =
      data?.detail ??
      data?.title ??
      data?.message ??
      data?.error ??
      error.message ??
      'An unexpected network error occurred';

    return Promise.reject(new Error(message));
  },
);

export default axiosClient;
