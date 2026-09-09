# Frontend Folder Structure

This document outlines the architecture, directory organization, and file responsibilities for the `frontend` application of the Smart Attendance System.

---

## Directory Tree

```
frontend/
├── .dockerignore
├── .env
├── .gitignore
├── Dockerfile
├── eslint.config.js
├── index.html
├── package.json
├── package-lock.json
├── README.md
├── vite.config.js
├── public/
└── src/
    ├── App.jsx
    ├── index.css
    ├── main.jsx
    ├── api/
    │   ├── axiosClient.js
    ├── components/
    │   ├── Footer/
    │   │   ├── Footer.jsx
    │   │   └── Footer.module.css
    │   ├── Navbar/
    │   │   ├── Navbar.jsx
    │   │   └── Navbar.module.css
    ├── constants/
    │   ├── roles.js
    │   └── withdrawal.js
    ├── context/
    │   ├── AuthContext.jsx
    │   ├── NotificationsContext.jsx
    │   └── useNotifications.js
    ├── hooks/
    │   ├── useApi.js
    │   ├── useCourses.js
    │   └── useTeacherCourseOptions.js
    ├── layouts/
    │   ├── AuthLayout.jsx
    │   ├── DashboardLayout.jsx
    │   ├── DashboardLayout.module.css
    │   └── RootLayout.jsx
    ├── pages/
    │   ├── LoginPage/
    │   │   ├── LoginPage.jsx
    │   │   └── LoginPage.module.css
    │   ├── NotFoundPage/
    │   │   └── NotFoundPage.jsx
    │   ├── PlaceholderPage/
    │   │   └── PlaceholderPage.jsx
    │   ├── RegisterPage/
    │   │   ├── RegisterPage.jsx
    │   │   └── RegisterPage.module.css
    │   ├── StudentCourses/
    │   │   ├── StudentCourses.jsx
    │   │   ├── StudentCourses.styles.js
    │   │   ├── useAttendanceExcuse.js
    │   │   └── useStudentCourses.js
    │   ├── StudentDashboard/
    │   │   ├── StudentDashboard.jsx
    │   │   ├── StudentDashboard.module.css
    │   │   └── useStudentDashboard.js
    │   ├── StudentProfile/
    │   │   ├── FaceEnrollmentDialog.jsx
    │   │   ├── StudentProfile.jsx
    │   │   ├── StudentProfile.styles.js
    │   │   ├── StudentProfileEditDialog.jsx
    │   │   ├── useFaceEnrollment.js
    │   │   └── useStudentProfile.js
    │   ├── StudentTakeAttendance/
    │   │   ├── QrScanner.jsx
    │   │   ├── StudentTakeAttendance.jsx
    │   │   ├── StudentTakeAttendance.styles.js
    │   │   └── useStudentTakeAttendance.js
    │   ├── StudentWithdrawal/
    │   │   ├── ExcuseHistoryList.jsx
    │   │   ├── ExcuseSubmissionForm.jsx
    │   │   ├── StudentWithdrawal.jsx
    │   │   ├── StudentWithdrawal.styles.js
    │   │   └── useStudentWithdrawal.js
    │   ├── TeacherAttendance/
    │   │   ├── AttendanceRosterRow.jsx
    │   │   ├── ExportAttendanceDialog.jsx
    │   │   ├── exportAttendanceReport.js
    │   │   ├── LiveSessionPanel.jsx
    │   │   ├── TeacherAttendance.jsx
    │   │   ├── TeacherAttendance.styles.js
    │   │   ├── useAttendanceExport.js
    │   │   ├── useAttendanceSession.js
    │   │   └── useTeacherAttendance.js
    │   ├── TeacherCourses/
    │   │   ├── TeacherCourses.jsx
    │   │   ├── TeacherCourses.module.css
    │   │   └── useTeacherCourses.js
    │   ├── TeacherDashboard/
    │   │   ├── exportDashboardReport.js
    │   │   ├── TeacherDashboard.jsx
    │   │   ├── TeacherDashboard.styles.js
    │   │   └── useTeacherDashboard.js
    │   ├── TeacherExcuseReview/
    │   │   ├── ExcuseDetailDialog.jsx
    │   │   ├── TeacherExcuseReview.jsx
    │   │   ├── TeacherExcuseReview.styles.js
    │   │   └── useTeacherExcuseReview.js
    │   ├── TeacherProfile/
    │   │   ├── TeacherProfile.jsx
    │   │   ├── TeacherProfile.styles.js
    │   │   ├── TeacherProfileEditDialog.jsx
    │   │   └── useTeacherProfile.js
    │   └── TeacherStudents/
    │       ├── TeacherStudents.jsx
    │       ├── TeacherStudents.module.css
    │       └── useTeacherStudents.js
    ├── routes/
    │   ├── ProtectedRoute.jsx
    │   ├── roleHelpers.js
    │   └── routes.jsx
    ├── styles/
    │   └── tokens.css
    ├── theme/
    │   └── theme.js
    └── utils/
        └── geolocation.js
```

---

## Architectural Breakdown

### 1. Root Configuration & Tooling

- `Dockerfile` & `.dockerignore`: Container packaging for frontend deployment.
- `eslint.config.js`: ESLint configuration for code quality.
- `index.html`: Main HTML entry point.
- `package.json` & `package-lock.json`: Dependencies and script definitions.
- `vite.config.js`: Vite build and development server configuration.
- `.env`: Environment variables (API base URL, endpoints).

### 2. Core Application Entry (`src/`)

- `main.jsx`: Mounts the React application to the DOM and initializes root providers.
- `App.jsx`: Configures React Router, theme provider, and global context providers.
- `index.css`: Global base styles and CSS reset.

### 3. API Services (`src/api/`)

Centralized API communication layer using Axios:

- `axiosClient.js`: Configured Axios instance with request and response interceptors.
- `auth.service.js`: Authentication endpoints (login, registration, tokens).
- `attendanceSession.service.js`: Live attendance session management.
- `downloadBlob.js`: Helper utility for downloading binary file responses (e.g., CSV/PDF exports).
- `faceAttendance.service.js`: Facial recognition verification and enrollment API calls.
- `notifications.service.js`: In-app notification retrieval and status updates.
- `studentCourses.service.js`: Student course enrollment and attendance history.
- `studentDashboard.service.js`: Aggregated metrics and overview data for students.
- `studentProfile.service.js`: Student profile details and face enrollment metadata.
- `studentWithdrawal.service.js`: Withdrawal status and excuse submission.
- `teacherAttendance.service.js`: Attendance session controls and manual overrides.
- `teacherCourses.service.js`: Courses assigned to the teacher.
- `teacherExcuses.service.js`: Excuse review workflows (approval/rejection).
- `teacherProfile.service.js`: Teacher profile details and updates.
- `teacherReports.service.js`: Report generation endpoints for teacher analytics.
- `teacherStudents.service.js`: Student roster inspection and course-level enrollment tracking.

### 4. Shared Components (`src/components/`)

Reusable presentational and functional UI components:

- `CameraCapture/`: Webcam capture interface for face recognition and enrollment.
- `ExcuseSubmissionDialog/`: Modal dialog for submitting medical or excused absence requests.
- `Footer/`: Application footer component with CSS module styles.
- `Navbar/`: Main header navigation bar including user controls and notification trigger.
- `NotificationBell/`: Icon indicator showing unread notification counts.
- `NotificationPopover/`: Popover panel rendering notification lists.

### 5. Constants (`src/constants/`)

Immutable definitions and shared enumeration constants:

- `attendance.js`: Attendance status codes and session states.
- `notifications.js`: Notification category types and intervals.
- `roles.js`: System roles (`Student`, `Teacher`, `Admin`).
- `withdrawal.js`: Withdrawal case statuses and excuse states.

### 6. State Contexts (`src/context/`)

React Context providers for global application state:

- `AuthContext.jsx`: Manages user authentication state, tokens, and active profile.
- `NotificationsContext.jsx`: Handles notification polling, real-time updates, and unread counts.
- `useNotifications.js`: Custom hook providing direct access to the notifications context.

### 7. Custom Hooks (`src/hooks/`)

Shared hooks encapsulating reusable asynchronous and query patterns:

- `useApi.js`: Generic API invocation wrapper managing loading, error, and data states.
- `useCourses.js`: Common hook for retrieving course collections.
- `useTeacherCourseOptions.js`: Formats teacher courses for selector dropdowns.

### 8. Layouts (`src/layouts/`)

Structural wrapper components for nested routing:

- `RootLayout.jsx`: Top-level layout housing global shell elements.
- `AuthLayout.jsx`: Centered container layout for authentication screens.
- `DashboardLayout.jsx` & `DashboardLayout.module.css`: Main dashboard shell with responsive navigation and content viewports.

### 9. Feature Pages (`src/pages/`)

Feature-scoped pages with dedicated sub-components, CSS modules/styles, and controller hooks:

#### Authentication & General

- `LoginPage/`: User login screen.
- `RegisterPage/`: Student and teacher onboarding form.
- `NotFoundPage/`: 404 error page.
- `PlaceholderPage/`: Temporary fallback page for pending routes.

#### Student Workflows

- `StudentDashboard/`: Student overview displaying attendance summaries and active courses.
- `StudentCourses/`: Course list with attendance records and excuse actions.
- `StudentProfile/`: Profile settings and face enrollment dialog.
- `StudentTakeAttendance/`: Multi-step attendance verification (QR scan, face verification, location check).
- `StudentWithdrawal/`: Withdrawal threshold warnings and excuse submission list.

#### Teacher Workflows

- `TeacherDashboard/`: Summary metrics and attendance trends.
- `TeacherCourses/`: Course management and enrolled student lists.
- `TeacherAttendance/`: Live attendance session launcher, QR code display, and roster verification.
- `TeacherExcuseReview/`: Review queue for student absence excuses.
- `TeacherProfile/`: Teacher account details and profile editing.
- `TeacherStudents/`: Student attendance monitoring and per-course records.

### 10. Routing (`src/routes/`)

Route configuration and route guarding:

- `routes.jsx`: Central declarative route definitions for React Router DOM.
- `ProtectedRoute.jsx`: Role-based route guard redirecting unauthorized users.
- `roleHelpers.js`: Utilities for checking role authorization and active permissions.

### 11. Styles & Theme (`src/styles/`, `src/theme/`)

- `styles/tokens.css`: CSS custom properties for spacing, colors, and shadows.
- `theme/theme.js`: Material-UI theme customization (palette, typography, component overrides).

### 12. Utilities (`src/utils/`)

- `geolocation.js`: Geolocation capture and radius verification helpers.
