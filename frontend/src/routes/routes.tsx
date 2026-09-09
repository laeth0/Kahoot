import { Navigate, Route, Routes } from 'react-router-dom';

import { AuthLayout } from '../layouts/AuthLayout.tsx';
import { RootLayout } from '../layouts/RootLayout.tsx';
import { HomePage } from '../pages/HomePage/HomePage.tsx';
import { HostDashboard } from '../pages/HostDashboard/HostDashboard.tsx';
import { LoginPage } from '../pages/LoginPage/LoginPage.tsx';
import { NotFoundPage } from '../pages/NotFoundPage/NotFoundPage.tsx';
import { ProtectedRoute } from './ProtectedRoute.tsx';

/**
 * Centralized application route definitions with nested layouts and protected routes.
 */
export function AppRoutes() {
  return (
    <Routes>
      {/* Public Pages with Root Navigation Shell */}
      <Route element={<RootLayout />}>
        <Route index element={<HomePage />} />
      </Route>

      {/* Host Authentication Layout */}
      <Route element={<AuthLayout />}>
        <Route path="login" element={<LoginPage />} />
      </Route>

      {/* Protected Host Area */}
      <Route path="host" element={<ProtectedRoute />}>
        <Route element={<RootLayout />}>
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<HostDashboard />} />
        </Route>
      </Route>

      {/* Catch-all 404 Page */}
      <Route element={<RootLayout />}>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

export default AppRoutes;
