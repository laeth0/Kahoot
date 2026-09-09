import { useEffect } from 'react';

import { useAuth } from './useAuth.ts';

const REFRESH_MARGIN_MS = 60 * 1000;
const MIN_TIMEOUT_MS = 5 * 1000;
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
        } catch (err) {
          void err;
        }
      }, delay);
    };

    scheduleRefresh();

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        const expiresTime = new Date(accessTokenExpiresAt).getTime();
        const now = Date.now();
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
