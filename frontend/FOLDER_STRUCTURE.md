# Frontend Folder Structure & Architecture Guide

This document outlines the standard, scalable folder structure and architectural conventions for the frontend application. It is designed for modern React 19 applications using TypeScript, Vite, Material UI (MUI), React Router DOM v7, Axios, and SignalR.

---

## 1. Visual Directory Tree

```text
frontend/
├── public/                         # Static assets served directly (favicon, manifest, robots.txt)
├── src/
│   ├── api/                        # API client, HTTP interceptors, and modular endpoint services
│   ├── assets/                     # Static media files imported into TypeScript/React (images, logos, SVGs)
│   ├── components/                 # Reusable, shared UI components used across multiple pages
│   ├── constants/                  # System-wide immutable values, enums, status codes, and limits
│   ├── context/                    # Application-wide React Context providers and consumers
│   ├── hooks/                      # Shared custom React hooks encapsulating stateful logic
│   ├── layouts/                    # Structural layout wrappers and routing shells (with <Outlet />)
│   ├── pages/                      # Feature views and route-level page modules
│   ├── realtime/                   # Real-time communication layer (SignalR/WebSocket clients and event contracts)
│   ├── routes/                     # React Router configuration, path definitions, and route guards
│   ├── styles/                     # Global CSS tokens, variables, font declarations, and resets
│   ├── theme/                      # Material UI (MUI) design system theme configuration
│   ├── utils/                      # Pure helper functions, formatting, and mathematical utilities
│   ├── App.tsx                     # Top-level application component with providers & routing root
│   ├── index.css                   # Global CSS entry file
│   └── main.tsx                    # Application entry point mounting React to the DOM
├── .env.example                    # Environment variable template
├── eslint.config.js                # ESLint configuration
├── index.html                      # HTML template entry point
├── package.json                    # Project dependencies and npm scripts
├── tsconfig.json                   # TypeScript configuration
└── vite.config.ts                  # Vite build and plugin configuration
```

---

## 2. Directory Responsibilities & Rules

### `src/api/`
**Purpose:** Centralizes all HTTP communication with the backend API.
- **What belongs here:**
  - Configured HTTP client instance (`axiosClient.ts`) with request/response interceptors (auth token injection, refresh-token handling, centralized ProblemDetails error extraction).
  - Feature-specific API service modules (`authService.ts`, `quizService.ts`, `quizQuestionService.ts`, `hostGameService.ts`, `uploadService.ts`).
  - Request and response DTO types (or re-exported contracts).
- **Rules:**
  - ❌ **Never** call `fetch` or `axios` directly inside React components or pages. Always call methods from `src/api/`.
  - ❌ **Never** place UI rendering, JSX, or component-specific state in this directory.
  - ✅ Handle HTTP protocol specifics (headers, status codes, abort signals) here or propagate typed errors.

---

### `src/assets/`
**Purpose:** Stores static media files that are imported directly into JavaScript/TypeScript code or CSS.
- **What belongs here:**
  - Brand assets (e.g., `logo.jpeg`, brand icons).
  - Illustrations, background images, vector graphics (SVGs), and sound effects.
- **Rules:**
  - ✅ Use this folder for assets that are bundled, hashed, and optimized by Vite.
  - ❌ For static files that must remain at a fixed URL without bundling (e.g., `favicon.ico`, `robots.txt`, manifest), place them in `frontend/public/` instead.

---

### `src/components/`
**Purpose:** Houses reusable, presentation-driven and functional UI components shared across multiple pages or layouts.
- **What belongs here:**
  - Global navigation and chrome: `Navbar`, `Footer`.
  - Shared domain widgets: `QuizCard`, `GamePinDisplay`, `ParticipantGrid`, `LeaderboardPodium`, `ServerCountdown`, `AnsweredCounter`.
  - Generic UI utilities: `ConfirmDialog`, `ErrorBoundary`, `Feedback`, `ImageUploadField`.
- **Colocation Pattern:**
  Each component directory should encapsulate its primary component, types, and sub-parts:
  ```text
  src/components/QuizCard/
  ├── QuizCard.tsx          # Component markup and behavior
  ├── index.ts              # Clean export: export * from './QuizCard';
  └── types.ts              # (Optional) Component-specific prop types
  ```
- **Rules:**
  - ❌ **Do not** put page-specific components here. If a component is only used on a single page (e.g., `QuizEditorPage`), colocate it inside `src/pages/<PageName>/components/`.
  - ✅ Use Material UI (MUI) components as the building blocks. Reuse theme tokens via `sx`.

---

### `src/constants/`
**Purpose:** Houses system-wide immutable configuration values, enums, status mappings, and validation constants.
- **What belongs here:**
  - Business status codes and enums: `gameStatus.ts` (e.g., `GameStatus` enum: Created, Lobby, QuestionActive, etc.).
  - Error code constants: `errorCodes.ts` (mapping backend error codes to frontend messages).
  - Form validation constants: `validation.ts` (min/max lengths, regex patterns).
  - Magic numbers: countdown limits, max choice counts, allowed file extensions.
- **Rules:**
  - ❌ No functions with side-effects or state. Only pure constants, enums, and frozen configuration objects.

---

### `src/context/`
**Purpose:** Manages cross-cutting, application-wide global state using React Context.
- **What belongs here:**
  - Authentication state: `AuthContext.ts` and `AuthProvider.tsx` (current user, access token in-memory, login/logout actions).
  - Global notification or modal state (if applicable).
- **Rules:**
  - ❌ **Do not** use Context for high-frequency or local state (e.g., typing in a text field, individual game countdown ticks).
  - ✅ Export a typed custom hook (e.g., `useAuth()`) that enforces the consumer is wrapped in its corresponding Provider.
  - ✅ Keep providers focused and split by domain responsibility.

---

### `src/hooks/`
**Purpose:** Shared custom React hooks that encapsulate reusable, stateful lifecycle logic, event listeners, or timers across multiple features.
- **What belongs here:**
  - Global auth hook: `useAuth.ts`, `useTokenRefresh.ts`.
  - Shared game orchestrators: `useHostGame.ts`, `usePlayerGame.ts`.
  - Hardware / Real-time hooks: `useGameHubConnection.ts`, `useServerCountdown.ts`, `useSessionToken.ts`.
  - Data-fetching hooks used across features: `useQuiz.ts`, `useQuizzes.ts`.
- **Rules:**
  - ❌ **No UI markup (JSX)** should be returned from custom hooks. Return state, computed values, and actions/callbacks.
  - ❌ If a hook is only needed by a single page, keep it within `src/pages/<PageName>/hooks/`. Move it to `src/hooks/` only when genuinely shared.
  - ✅ Always prefix hook names with `use` (e.g., `useHostGame`).

---

### `src/layouts/`
**Purpose:** Structural wrapper shells that define layout frames (navbars, sidebars, footers, containers) for nested routes.
- **What belongs here:**
  - `RootLayout.tsx`: Top-level application shell, mounting global providers and alert banners.
  - `AuthLayout.tsx`: Centered, distraction-free container for authentication (Login).
  - `GameLayout.tsx`: Full-screen, responsive canvas optimized for active live gameplay and participant/host views.
- **Rules:**
  - ✅ Layouts must render React Router's `<Outlet />` to render active child routes.
  - ❌ Avoid putting heavy domain-specific logic in layouts; layouts should focus on structural shell composition.

---

### `src/pages/`
**Purpose:** Route-level views representing distinct screens in the application.
- **What belongs here:**
  - Feature pages: `HomePage`, `LoginPage`, `HostDashboard`, `QuizEditorPage`, `CreateQuizPage`, `QuizLibraryPage`, `JoinPage`, `HostGamePage`, `PlayerGamePage`, `NotFoundPage`.
- **Page Encapsulation Pattern:**
  Keep page-scoped sub-components, page-specific hooks, and local types inside the owning page folder:
  ```text
  src/pages/QuizEditorPage/
  ├── QuizEditorPage.tsx       # Main page view
  ├── components/              # Sub-components used ONLY on this page
  │   ├── QuestionDrawer.tsx
  │   └── SettingsModal.tsx
  ├── hooks/                   # Custom hooks used ONLY on this page
  │   └── useQuizAutosave.ts
  ├── types.ts                 # Types specific to this page
  └── index.ts                 # Entry export
  ```
- **Rules:**
  - ✅ Each page represents a navigable route.
  - ❌ Do not import sub-components from another page's private directory. If another page needs a sub-component, promote it to `src/components/`.

---

### `src/realtime/`
**Purpose:** Dedicated layer for real-time WebSockets / SignalR connection management and event messaging contracts.
- **What belongs here:**
  - SignalR Hub client setup: `gameHub.ts` (connection building, transport configuration, start/stop lifecycle, state listeners).
  - Event contract definitions: `events.ts` (typed server-to-client events, client-to-server invocations, payload interfaces).
- **Rules:**
  - ✅ Keeps real-time networking mechanics decoupled from React UI rendering.
  - ✅ React components interact with `realtime/` via dedicated hooks (e.g., `useGameHubConnection`).
  - ❌ Do not mix REST API endpoints into this directory; REST belongs in `src/api/`.

---

### `src/routes/`
**Purpose:** Centralizes client-side routing definitions, navigation guards, and route configuration.
- **What belongs here:**
  - Route tree configuration: `routes.tsx` (using React Router DOM v7 declarative routes or route objects).
  - Route guards: `ProtectedRoute.tsx` (ensuring host authentication before accessing `/dashboard`, `/quizzes`, etc.).
  - Route path constants and navigation helpers.
- **Rules:**
  - ✅ Group routes by layout shells (`RootLayout`, `AuthLayout`, `GameLayout`).
  - ✅ Configure error elements or fallback boundaries at the route level.

---

### `src/styles/`
**Purpose:** Global styles, CSS resets, font declarations, and design system tokens.
- **What belongs here:**
  - `tokens.css`: CSS custom properties (`--color-primary`, `--font-family`, `--spacing-unit`, elevation variables).
- **Rules:**
  - ❌ Do not write large global style sheets that target HTML elements directly.
  - ✅ Use CSS variables to bridge system-wide design tokens between CSS and Material UI theme overrides.

---

### `src/theme/`
**Purpose:** Material UI (MUI) design system theme configuration.
- **What belongs here:**
  - `palette.ts`: Color definitions (Primary, Secondary, Background, Text, Status colors).
  - `typography.ts`: Font families, font sizes, line heights, and font weights.
  - `components.ts`: MUI component overrides (`MuiButton`, `MuiCard`, `MuiTextField`, etc.).
  - `index.ts`: Aggregates and creates the theme instance with `createTheme()`.
- **Rules:**
  - ✅ In this repository, the theme is strictly **Light Mode only** (`mode: 'light'`).
  - ✅ Customize component visuals through theme overrides rather than writing ad-hoc CSS classes.
  - ❌ Avoid hardcoding raw hex values in individual components; reference theme palette tokens via MUI's `sx` prop.

---

### `src/utils/`
**Purpose:** Pure, stateless helper functions and utility libraries.
- **What belongs here:**
  - Clipboard helpers: `clipboard.ts` (copying game links, game PINs).
  - Ranking & Math algorithms: `rank.ts` (computing leaderboard placement, ordinal suffixes).
  - Formatting helpers: Date/time formatters, string slugifiers, number formatters.
- **Rules:**
  - ✅ All functions in `src/utils/` must be **pure functions** (same input = same output, no side effects).
  - ❌ Never import React, hooks, JSX, or component state into `src/utils/`.

---

## 3. Decision Matrix: Where Does My Code Go?

| What are you adding? | Target Directory | Example |
| :--- | :--- | :--- |
| An API endpoint call to fetch or mutate server data | `src/api/` | `quizService.ts` |
| An image, SVG icon, or audio clip used in components | `src/assets/` | `logo.jpeg` |
| A reusable UI button, modal, or input used across pages | `src/components/` | `src/components/ConfirmDialog/` |
| A UI component used on only one specific page | `src/pages/<PageName>/components/` | `src/pages/QuizEditorPage/components/ChoiceRow.tsx` |
| System status codes, magic numbers, or error code maps | `src/constants/` | `src/constants/gameStatus.ts` |
| App-wide state that multiple distant components need | `src/context/` | `src/context/AuthProvider.tsx` |
| Reusable stateful logic, timers, or window listeners | `src/hooks/` | `src/hooks/useServerCountdown.ts` |
| Page wrapper shell with persistent navigation / header | `src/layouts/` | `src/layouts/GameLayout.tsx` |
| A new route view / screen accessible by a URL | `src/pages/` | `src/pages/QuizLibraryPage/` |
| SignalR event definitions, hub connections, or socket listeners | `src/realtime/` | `src/realtime/gameHub.ts` |
| A new route path or auth guard wrapper | `src/routes/` | `src/routes/routes.tsx` |
| Global CSS variables or design token declarations | `src/styles/` | `src/styles/tokens.css` |
| MUI palette colors, typography scale, or component overrides | `src/theme/` | `src/theme/palette.ts` |
| A pure helper function (formatting, math, clipboard) | `src/utils/` | `src/utils/clipboard.ts` |

---

## 4. Naming Conventions

- **React Components & Layouts:** `PascalCase.tsx` (e.g., `QuizCard.tsx`, `RootLayout.tsx`, `LoginPage.tsx`)
- **Custom Hooks:** `camelCase.ts` starting with `use` (e.g., `useHostGame.ts`, `useAuth.ts`)
- **Services & Utilities:** `camelCase.ts` (e.g., `quizService.ts`, `clipboard.ts`, `axiosClient.ts`)
- **Constants & Enums:** `camelCase.ts` for files (`gameStatus.ts`), `UPPER_SNAKE_CASE` or `PascalCase` for exported enum/constant definitions
- **Component Folders:** `PascalCase/` matching the main component name (e.g., `src/components/Navbar/Navbar.tsx`)
- **Barrel Exports:** Use `index.ts` inside component or page folders to keep imports clean:
  ```typescript
  export * from './QuizCard';
  ```
  Allowing consumers to import with:
  ```typescript
  import { QuizCard } from '@/components/QuizCard';
  ```
