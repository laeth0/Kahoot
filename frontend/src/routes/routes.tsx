import { Navigate, Route, Routes } from 'react-router-dom';

import { AuthLayout } from '../layouts/AuthLayout.tsx';
import { RootLayout } from '../layouts/RootLayout.tsx';
import { CreateQuizPage } from '../pages/CreateQuizPage/index.ts';
import { HomePage } from '../pages/HomePage/HomePage.tsx';
import { JoinPage } from '../pages/JoinPage/JoinPage.tsx';
import { LoginPage } from '../pages/LoginPage/LoginPage.tsx';
import { NotFoundPage } from '../pages/NotFoundPage/NotFoundPage.tsx';
import { QuizEditorPage } from '../pages/QuizEditorPage/index.ts';
import { QuizLibraryPage } from '../pages/QuizLibraryPage/index.ts';
import { ProtectedRoute } from './ProtectedRoute.tsx';

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<RootLayout />}>
        <Route index element={<HomePage />} />
        <Route path="join" element={<JoinPage />} />
      </Route>

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
      </Route>

      <Route element={<RootLayout />}>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

export default AppRoutes;
