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

## Quiz question ordering

The quiz editor renders a six-dot drag handle for every question. Drag near the top or bottom
of the viewport to scroll through long quizzes while keeping the floating question visible.
The blue destination marker identifies the question to insert before, or the end of the list.

Keyboard users can focus a handle, press Space or Enter to pick up, use the arrow keys
(or Home/End), and press Space or Enter again to drop. Escape cancels. The up/down buttons
also remain available.

Each drop saves the complete question ID sequence through the existing question-order API.
Visible positions update immediately; question IDs and contents remain unchanged. Further
reorders wait for the save to finish, failures restore the previous order, and a saved reorder
returns the quiz to Draft status.

## Folder Structure

For an exhaustive architectural guide and component placement rules, see [FOLDER_STRUCTURE.md](file:///c:/Users/laeth/Desktop/kahoot/frontend/FOLDER_STRUCTURE.md).

```text
frontend/
├── public/                     # Static public assets (favicons, manifest, etc.)
├── src/
│   ├── api/                    # Centralized API clients and feature services
│   ├── assets/                 # Static images, icons, and media files imported in code
│   ├── components/             # Reusable shared UI components
│   ├── constants/              # Immutable enums, system roles, and status constants
│   ├── context/                # React Context providers for global application state
│   ├── hooks/                  # Reusable custom React hooks
│   ├── layouts/                # Structural layout wrappers for nested routing
│   │   ├── AuthLayout.tsx      # Centered layout for authentication views
│   │   ├── GameLayout.tsx      # Immersive layout for live quiz gameplay
│   │   └── RootLayout.tsx      # Top-level application shell layout
│   ├── pages/                  # Feature-scoped views and page modules
│   │   ├── CreateQuizPage/     # Quiz creation view
│   │   ├── HomePage/           # Application landing page
│   │   ├── HostDashboard/      # Host quiz management dashboard
│   │   ├── HostGamePage/       # Live host game control view
│   │   ├── JoinPage/           # Participant game PIN & nickname entry
│   │   ├── LoginPage/          # Host authentication view
│   │   ├── NotFoundPage/       # 404 route fallback
│   │   ├── PlayerGamePage/     # Participant live game view
│   │   ├── QuizEditorPage/     # Quiz question editor view
│   │   └── QuizLibraryPage/    # Quiz collection view
│   ├── realtime/               # SignalR hub connection and real-time event definitions
│   ├── routes/                 # Route configuration and navigation guards
│   ├── styles/                 # Design tokens, CSS variables, and global resets
│   ├── theme/                  # Material UI theme setup (palette, typography, overrides)
│   ├── utils/                  # Shared helper functions and utility libraries
│   ├── App.tsx                 # Root application component
│   ├── index.css               # Global base styles and font declarations
│   └── main.tsx                # Application entry point mounting to the DOM
├── .env.example                # Environment variable configuration template
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

Detailed guidelines, colocation patterns, and code placement rules are available in [FOLDER_STRUCTURE.md](file:///c:/Users/laeth/Desktop/kahoot/frontend/FOLDER_STRUCTURE.md).

### `src/api/`
Houses all remote communication logic. Contains the configured Axios client instance (`axiosClient.ts`) with request/response interceptors (auth token injection, refresh-token rotation, centralized problem details extraction) and modular domain services (`authService.ts`, `quizService.ts`, `hostGameService.ts`, etc.). Feature components do not call Axios directly; they consume methods from this layer.

### `src/assets/`
Contains static media assets (logos, images, illustrations, audio) bundled and optimized by Vite.

### `src/components/`
Contains reusable presentational and functional UI components used across multiple pages and layouts. Each component directory colocates its component code, sub-components, and exports (e.g., `ConfirmDialog/`, `Navbar/`, `ParticipantGrid/`, `ServerCountdown/`).

### `src/constants/`
Stores immutable values, system enumerations, and configuration constants (such as `GameStatus`, error codes, validation limits).

### `src/context/`
Contains React Context providers managing application-wide cross-cutting state (`AuthContext.ts`, `AuthProvider.tsx`).

### `src/hooks/`
Houses shared custom React hooks that encapsulate reusable stateful behavior and timers (`useAuth`, `useHostGame`, `usePlayerGame`, `useServerCountdown`, `useSessionToken`).

### `src/layouts/`
Provides structural wrapper components used by React Router to compose nested UI layouts (`RootLayout.tsx`, `AuthLayout.tsx`, `GameLayout.tsx`).

### `src/pages/`
Contains all feature pages organized by domain view. Each page folder encapsulates the main page component and page-scoped subcomponents, hooks, or types.

### `src/realtime/`
Encapsulates real-time SignalR connection lifecycle (`gameHub.ts`) and typed hub event contracts (`events.ts`).

### `src/routes/`
Centralizes client routing definitions with React Router DOM v7 (`routes.tsx`), path constants, and route guards (`ProtectedRoute.tsx`).

### `src/styles/`
Contains global CSS variables, typography tokens, and CSS reset rules (`tokens.css`).

### `src/theme/`
Contains the custom Material UI theme definition (`palette.ts`, `typography.ts`, `components.ts`, `index.ts`), strictly configured for light mode.

### `src/utils/`
Contains pure utility and helper functions (e.g., clipboard helpers, rank calculation, formatters) that do not hold state or render UI.

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
