# Frontend Application

A modern, responsive, client-rendered React application built with **React 19**, **TypeScript**, **Vite**, **Material UI (MUI)**, **React Router DOM 7**, **Axios**, **Zod**, and **SignalR**.

---

## Technology Stack

- **Framework & Runtime:** React 19, TypeScript, Vite
- **UI Library & Styling:** Material UI (MUI v9) with Emotion (`@emotion/react`, `@emotion/styled`), `@mui/icons-material`
- **Routing:** React Router DOM (v7)
- **Data Fetching:** Axios client with centralized interceptors
- **Real-Time Communication:** `@microsoft/signalr`
- **Schema Validation:** Zod
- **Code Quality & Formatting:** ESLint with `simple-import-sort` and Prettier (`eslint-config-prettier`)

---

## Folder Structure

```text
frontend/
├── public/                     # Static public assets (favicons, manifest, etc.)
├── src/
│   ├── api/                    # Centralized API clients and feature services
│   ├── assets/                 # Static images, icons, and media files imported in code
│   ├── components/             # Reusable shared UI components
│   │   ├── CameraCapture/      # Camera capture interface
│   │   ├── ExcuseSubmissionDialog/ # Modal for submitting absence/excuse requests
│   │   ├── Footer/             # Application footer
│   │   ├── Navbar/             # Main navigation bar with user actions
│   │   ├── NotificationBell/   # Real-time notification indicator
│   │   └── NotificationPopover/# Popover menu for unread notifications
│   ├── constants/              # Immutable enums, system roles, and status constants
│   ├── context/                # React Context providers for global application state
│   ├── hooks/                  # Reusable custom React hooks
│   ├── layouts/                # Structural layout wrappers for nested routing
│   │   ├── AuthLayout          # Layout for login, register, and onboarding screens
│   │   ├── DashboardLayout     # Layout for authenticated navigation and content views
│   │   └── RootLayout          # Top-level shell layout
│   ├── pages/                  # Feature-scoped views and page modules
│   │   ├── LoginPage/          # Host/user authentication view
│   │   ├── NotFoundPage/       # 404 route fallback
│   │   ├── PlaceholderPage/    # Under-construction / pending views
│   │   ├── RegisterPage/       # User registration and onboarding view
│   │   ├── StudentCourses/     # Course listings and attendance records
│   │   ├── StudentDashboard/   # Main student overview and metrics
│   │   ├── StudentProfile/     # Student settings and face enrollment
│   │   ├── StudentTakeAttendance/ # Multi-step attendance verification
│   │   ├── StudentWithdrawal/  # Absence excuses and withdrawal management
│   │   ├── TeacherAttendance/  # Attendance session controls and roster verification
│   │   ├── TeacherCourses/     # Teacher course management
│   │   ├── TeacherDashboard/   # Teacher metrics and trend overview
│   │   ├── TeacherExcuseReview/# Excuse review and approval queue
│   │   ├── TeacherProfile/     # Teacher account settings
│   │   └── TeacherStudents/    # Student roster inspection and course enrollment
│   ├── routes/                 # Route configuration and navigation guards
│   ├── styles/                 # Design tokens, CSS variables, and global resets
│   ├── theme/                  # Material UI theme setup (palette, typography, overrides)
│   ├── utils/                  # Shared helper functions and utility libraries
│   ├── App.css                 # Application-level styles
│   ├── App.tsx                 # Root application component
│   ├── index.css               # Global base styles and font declarations
│   └── main.tsx                # Application entry point mounting to the DOM
├── .prettierignore             # Files ignored by Prettier
├── .prettierrc                 # Prettier formatting configuration
├── eslint.config.js            # ESLint flat configuration (plugins, import sorting)
├── index.html                  # HTML entry template
├── package.json                # Project dependencies and npm scripts
├── tsconfig.json               # Root TypeScript configuration
├── tsconfig.app.json           # Application TypeScript configuration
├── tsconfig.node.json          # Node/Vite build TypeScript configuration
└── vite.config.ts              # Vite configuration and build plugins
```

---

## Directory Responsibilities

### `src/api/`

Houses all remote communication logic. Contains the configured Axios client instance (`axiosClient.js`) with request/response interceptors (auth token injection, error handling), SignalR connection helpers, and modular domain services (e.g., auth, courses, attendance). Feature components do not call Axios directly; they consume methods from this layer.

### `src/components/`

Contains reusable presentational and functional UI components used across multiple pages and layouts. Each component directory colocates its component code, styles, and sub-components (e.g., `Navbar/Navbar.tsx`, `Footer/Footer.tsx`).

### `src/constants/`

Stores immutable values, system enumerations, and configuration constants (such as role definitions `Admin`, `Teacher`, `Student`, session states, status codes, and query limits).

### `src/context/`

Contains React Context providers managing application-wide cross-cutting state (e.g., `AuthContext` for user session tokens, `NotificationsContext` for live alerts).

### `src/hooks/`

Houses shared custom React hooks that encapsulate reusable stateful behavior, such as `useApi` for request lifecycle management, or common query and event hooks.

### `src/layouts/`

Provides structural wrapper components used by React Router to compose nested UI layouts:

- `RootLayout`: Wraps the entire application with providers and metadata.
- `AuthLayout`: Centered layout for unauthenticated flows.
- `DashboardLayout`: Responsive sidebar/app-bar shell for authenticated features.

### `src/pages/`

Contains all feature pages organized by domain view. Each page folder encapsulates:

- The main page component (e.g., `StudentDashboard.tsx`)
- Page-scoped sub-components (e.g., dialogs, custom cards)
- Page-scoped custom hooks (e.g., `useStudentDashboard.ts`)
- Page-specific styling overrides

### `src/routes/`

Centralizes all client routing definitions with React Router DOM v7:

- Route declarations and nested hierarchies
- `ProtectedRoute`: Role-based route guards redirecting unauthorized visitors
- Role authorization helpers

### `src/styles/`

Contains global CSS variables, typography tokens, and CSS reset rules (`tokens.css`).

### `src/theme/`

Contains the custom Material UI theme definition (`theme.js`), configuring the design system's palette, typography hierarchy, shape border-radii, spacing tokens, and custom component style overrides.

### `src/utils/`

Contains pure utility and helper functions (e.g., date formatters, string parsers, geolocation utilities) that do not hold state or render UI.

---

## Available Scripts

| Command                | Description                                                                          |
| :--------------------- | :----------------------------------------------------------------------------------- |
| `npm run dev`          | Starts the Vite development server with Hot Module Replacement (HMR).                |
| `npm run build`        | Type-checks with `tsc` and bundles the production app with Vite.                     |
| `npm run preview`      | Locally previews the production build output from `dist/`.                           |
| `npm run lint`         | Runs ESLint across all source files to catch syntax, style, and import order issues. |
| `npm run lint:fix`     | Automatically fixes lint errors and sorts all `import` and `export` statements.      |
| `npm run format`       | Formats the entire codebase using Prettier.                                          |
| `npm run format:check` | Checks whether files adhere to the Prettier formatting rules without modifying them. |
