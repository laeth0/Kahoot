import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';

import { LoadingState } from '../components/Feedback/index.ts';
import { AuthLayout } from '../layouts/AuthLayout.tsx';
import { RootLayout } from '../layouts/RootLayout.tsx';
import { ProtectedRoute } from './ProtectedRoute.tsx';

const HomePage = lazy(() => import('../pages/HomePage/HomePage.tsx'));
const JoinPage = lazy(() => import('../pages/JoinPage/JoinPage.tsx'));
const LoginPage = lazy(() => import('../pages/LoginPage/LoginPage.tsx'));
const NotFoundPage = lazy(() => import('../pages/NotFoundPage/NotFoundPage.tsx'));
const PlayerGamePage = lazy(() => import('../pages/PlayerGamePage/index.ts'));
const QuizLibraryPage = lazy(() => import('../pages/QuizLibraryPage/index.ts'));
const CreateQuizPage = lazy(() => import('../pages/CreateQuizPage/index.ts'));
const QuizEditorPage = lazy(() => import('../pages/QuizEditorPage/index.ts'));
const HostGamePage = lazy(() => import('../pages/HostGamePage/index.ts'));

export function AppRoutes() {
  return (
    <Suspense fallback={<LoadingState variant="page" message="Loading page..." />}>
      <Routes>
        <Route element={<RootLayout />}>
          <Route index element={<HomePage />} />
          <Route path="join" element={<JoinPage />} />
        </Route>

        <Route path="play/:gameId" element={<PlayerGamePage />} />

        <Route element={<AuthLayout />}>
          <Route path="login" element={<LoginPage />} />
        </Route>

        <Route path="host" element={<ProtectedRoute />}>
          <Route element={<RootLayout />}>
            <Route index element={<Navigate to="quizzes" replace />} />
            <Route path="dashboard" element={<Navigate to="/host/quizzes" replace />} />
            <Route path="quizzes" element={<QuizLibraryPage />} />
            <Route path="quizzes/new" element={<CreateQuizPage />} />
            <Route path="quizzes/:quizId" element={<QuizEditorPage />} />
          </Route>
          <Route path="game/:gameId" element={<HostGamePage />} />
        </Route>

        <Route element={<RootLayout />}>
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </Suspense>
  );
}

export default AppRoutes;
