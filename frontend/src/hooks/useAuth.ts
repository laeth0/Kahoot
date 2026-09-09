import { useContext } from 'react';

import { AuthContext, type AuthContextType } from '../context/AuthContext.tsx';

/**
 * Custom React Hook providing access to Host authentication context.
 */
export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

export default useAuth;
