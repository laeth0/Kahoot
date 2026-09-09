import { useEffect } from 'react';

import { useAuth } from './useAuth.ts';

const REFRESH_MARGIN_MS = 60 * 1000; // Refresh 60 seconds before expiration
const MIN_TIMEOUT_MS = 5 * 1000; // Minimum 5s backoff

/**
 * Hook for automatic, silent refresh of the host's access token before it expires.
 * Also checks token freshness whenever the browser tab becomes visible.
 */
export function useTokenRefresh() {
  const { isAuthenticated, accessTokenExpiresAt, refreshToken, refreshSession } = useAuth();

  useEffect(() => {
    if (!isAuthenticated || !refreshToken || !accessTokenExpiresAt) {
      return;
    }

    let timerId: ReturnType<typeof setTimeout> | null = null;

    const scheduleRefresh = () => {
      if (timerId) {
        clearTimeout(timerId);
        timerId = null;
      }

      const expiresTime = new Date(accessTokenExpiresAt).getTime();
      const now = Date.now();
      const delay = Math.max(expiresTime - now - REFRESH_MARGIN_MS, MIN_TIMEOUT_MS);

      timerId = setTimeout(async () => {
        try {
          await refreshSession();
        } catch {
          // Failure handling is managed within refreshSession (auto-logout)
        }
      }, delay);
    };

    scheduleRefresh();

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        const expiresTime = new Date(accessTokenExpiresAt).getTime();
        const now = Date.now();
        // If expired or within the margin, refresh immediately
        if (expiresTime - now <= REFRESH_MARGIN_MS) {
          refreshSession();
        } else {
          scheduleRefresh();
        }
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      if (timerId) clearTimeout(timerId);
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [isAuthenticated, accessTokenExpiresAt, refreshToken, refreshSession]);
}

export default useTokenRefresh;
