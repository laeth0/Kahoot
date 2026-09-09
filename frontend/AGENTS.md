# React Engineering Standards

These rules apply to React work in this repository and extend the repository-root
`AGENTS.md`; apply all root rules without restating them here. Keep this file limited to
additional React, MUI, React Router, Axios, accessibility, and SEO requirements for this
client-rendered application.

## React Scope and Version

- Confirm the target package uses React by inspecting `package.json`, the application
  entry point, and the build configuration before applying these rules.
- Use React APIs supported by the installed version. Do not introduce React 19 APIs,
  React Compiler assumptions, or framework-specific rendering primitives into a project
  that has not enabled them.
- Keep standalone React guidance separate from Next.js conventions. Do not add
  Next.js-only directives, Route Handlers, App Router files, metadata APIs, or caching
  APIs to a client-rendered React application.

## Required Project Stack and Boundaries

- Treat this project as a client-rendered React application using Material UI, React
  Router DOM 7, and the established Axios client layer.
- Use MUI as the required UI component library and React Router DOM 7 as the required
  client-routing library. Do not introduce a competing component library, design system,
  router, or full-stack React framework unless the user explicitly requests a migration.
- Do not introduce a parallel request client or proxy abstraction when the established
  Axios client and feature API modules already provide the required boundary.

## Application Entry Point and Root

- Mount the client-rendered application with `createRoot` or the equivalent API supported
  by the installed React version. Do not introduce hydration or another rendering mode
  unless the user explicitly changes the application's rendering architecture.
- Keep root setup focused on application-wide providers, the router, error reporting,
  strict-mode behavior, and global styles. Do not place feature logic in the entry file.
- Preserve `StrictMode` when the project uses it. Fix effects and cleanup logic that
  fail under development remounting instead of disabling Strict Mode to hide defects.
- Keep global providers ordered intentionally. Place providers as close as practical to
  their consumers so unrelated routes and components do not subscribe to their updates.
- Keep the initial client render deterministic and ensure root-level providers do not
  trigger duplicate initialization, requests, subscriptions, or navigation.

## Material UI and Visual Consistency

> **Always use Material UI (MUI) as the primary source of UI components on every page.**

- Build pages with established MUI components such as `Box`, `Container`, `Stack`,
  `Grid`, `Paper`, `Card`, `Typography`, `Button`, `TextField`, `Avatar`, `Chip`,
  `Table`, `Dialog`, `Alert`, `Skeleton`, and the appropriate semantic component for
  each interaction.
- Do not build raw HTML-only interfaces when MUI provides the required component. Use
  MUI's `component` prop when semantic HTML and MUI styling must be combined.
- Reuse the existing MUI theme's layouts, spacing, typography, colors, variants,
  responsive patterns, icons, states, and interaction behavior.
- Use theme palette, typography, spacing, shape, breakpoint, and component tokens instead
  of hardcoded visual values. Extend the theme when a reusable product-level token or
  variant is required; do not bypass it with a separate design system.
- Prefer the `sx` prop for component-scoped styling and the installed MUI version's
  `slots` and `slotProps` APIs for supported component customization. Follow existing
  theme override and `styled` conventions where they already own reusable styling.
- Import MUI components through the public package entry points supported by the
  installed version. Do not rely on unsupported deep imports.
- Use CSS Modules only for layout-level overrides or styling that MUI's `sx` prop cannot
  express cleanly. Keep each override scoped and do not duplicate theme tokens in CSS.
- Type reusable `sx` objects with `SxProps<Theme>` in TypeScript. Keep simple styles near
  their component and move a large cohesive style map to a colocated styles file when
  that materially improves readability.
- Use MUI icons and established icon-button patterns. Accessible names for icon-only
  controls and non-color state indication are specified in *Accessibility (WCAG 2.2
  Level AA)*.

## Components and Composition

- Use function components unless the existing codebase has a justified class-component
  boundary, such as an established error boundary on a React version without an
  equivalent supported alternative.
- Prefer composition, explicit props, and `children` over inheritance, hidden module
  state, or overly generic configuration objects.
- Keep rendering pure. Do not mutate props, state, context values, or external objects;
  perform side effects; read unstable external state; or update state during render.
- Do not define components inside other components. Stable component identity is
  required to preserve state and avoid unnecessary unmounting.
- Keep component props limited to the component's render contract rather than forwarding
  large records or spreading unrelated props.
- Use fragments only when an extra DOM element has no semantic or layout purpose. Keep
  meaningful semantic elements in the rendered structure.
- Use stable domain identifiers as list keys. Do not use array indexes when items can be
  inserted, removed, filtered, sorted, or reordered, and do not generate keys during
  render.

## Rules of Hooks

- Call Hooks only at the top level of React function components or custom Hooks. Never
  call Hooks conditionally, inside loops, event handlers, nested functions, class
  components, or ordinary utility functions.
- Name custom Hooks with the `use` prefix and keep their public return contract stable
  and explicit.
- Do not hide a Hook call behind a function whose name and contract imply an ordinary
  utility.
- Keep Hook dependency lists complete and accurate. Do not disable the Hooks linter or
  omit dependencies to force a desired execution schedule.
- Use the React Hooks ESLint rules configured by the repository and address their
  findings by correcting ownership, dependencies, or control flow.
- Use the `use` API only when the installed React version and rendering environment
  support it. Unlike ordinary Hooks, follow its documented conditional and loop
  semantics without wrapping it in `try/catch`.

## State Ownership

- Keep state in the lowest component that owns the behavior. Lift it only to the nearest
  common owner that must coordinate multiple consumers.
- Store the smallest authoritative state. Derive filtered lists, totals, flags, and
  other computable values during render instead of synchronizing duplicate state.
- Use `useState` for independent values and `useReducer` when related transitions form
  a state machine or benefit from one explicit transition function.
- Model mutually exclusive UI states with a discriminated union or reducer rather than
  combinations of booleans that permit impossible states.
- Never mutate arrays, objects, Maps, Sets, or nested state in place. Produce a new value
  for every changed state path.
- Use functional state updates when the next value depends on the previous value,
  especially across batched or deferred updates.
- Do not reset or synchronize state through an effect when component identity, a key,
  controlled props, or deriving during render expresses the lifecycle correctly.
- Preserve state intentionally according to component position and keys. Change a key
  only when resetting the entire subtree is the desired behavior.

## Effects and External Synchronization

- Use `useEffect` only to synchronize React with an external system, such as a browser
  API, network subscription, timer, event source, widget, or non-React store.
- Do not use an effect to calculate render data, respond to a user event, chain internal
  state updates, notify a parent of state it already owns, or copy props into state.
- Include every reactive value read by an effect in its dependency list. Restructure the
  code when a dependency causes unwanted reruns rather than suppressing it.
- Return cleanup logic for subscriptions, listeners, observers, timers, connections, and
  in-flight work. Cleanup must fully undo the corresponding setup and remain safe when
  setup and cleanup run repeatedly.
- Prevent stale asynchronous results from overwriting newer state. Use the API client's
  cancellation mechanism, an `AbortController`, or an explicit ignore lifecycle where
  appropriate.
- Use `useLayoutEffect` only for synchronous layout measurement or DOM mutation that
  must occur before paint. Prefer `useEffect` for all other external synchronization.
- Use `useEffectEvent` only when supported by the installed React version and when
  non-reactive effect logic genuinely needs access to current values.

## Refs and Imperative Behavior

- Use `useRef` for DOM access or mutable values that must persist without causing a
  render. Do not use refs as a parallel state system for values shown in the UI.
- Read or write refs from event handlers, effects, or supported ref callbacks rather
  than during render, except for documented idempotent initialization patterns.
- Expose imperative component behavior only when a declarative prop contract cannot
  express the requirement.
- Keep imperative handles narrow and stable. Do not expose a component's internal DOM
  structure or implementation details unnecessarily.
- Use the ref model supported by the installed React version. Do not add legacy
  `forwardRef` patterns to code targeting a version whose established project
  conventions use ref-as-prop, and do not backport newer semantics to older React.

## Context and Shared State

- Use context for values genuinely shared across a subtree, such as the active theme,
  authenticated-user view, locale, or a stable service boundary.
- Do not place rapidly changing or unrelated values into one broad context. Split
  contexts by responsibility when consumers need independent update behavior.
- Keep provider values stable when practical. Do not create a new object or callback on
  every render when it causes broad, measurable consumer rerenders.
- Provide a typed custom Hook for consuming context when it should enforce provider
  presence or hide internal context structure.
- Use an external state library only when the existing application already relies on it
  or the required cross-tree update, subscription, or persistence behavior cannot be
  handled clearly with local state, reducers, and context.
- For external mutable stores, use the repository's established React integration or
  `useSyncExternalStore`; do not subscribe through ad hoc render-time reads.

## Custom Hooks

- Extract a custom Hook when multiple components share the same stateful lifecycle or
  when one component's Hook orchestration forms a distinct responsibility.
- Keep UI markup out of custom Hooks. Return the smallest state and operations needed by
  consumers.
- Do not extract a custom Hook solely to hide a single trivial `useState` or
  `useEffect` call.
- Keep each custom Hook independent of component call order beyond the Rules of Hooks.
  Do not rely on mutable module variables to coordinate Hook instances.
- Return stable callbacks only when consumers require stable identity. Avoid wrapping
  every returned function in `useCallback` by default.

## Project File Organization

- Keep page-specific components, Hooks, helpers, types, and styles inside their owning
  `src/pages/<PageName>/` directory.
- Move a Hook to `src/hooks/` only when multiple features or pages genuinely share its
  stateful responsibility and contract.
- Keep reusable application components directly under
  `src/components/<ComponentName>/`. Do not introduce a generic
  `src/components/layout/` wrapper directory.
- Keep feature API calls in the established feature API modules and API contracts near
  the layer that owns them. Do not place Axios calls or response-shaping logic in MUI
  presentation components.
- Reuse shared API contract and domain types instead of recreating equivalent shapes in
  multiple pages. Keep page-only types within the owning page directory.

## Events and Forms

- Put interaction-driven logic in event handlers rather than effects. Name handlers for
  the user action or domain event they process.
- Use React's controlled or uncontrolled form model intentionally. Do not switch an
  input between controlled and uncontrolled during its lifetime.
- Keep one authoritative owner for each field value. Avoid duplicating the same form
  value in component state, a form library, and derived state.
- Use MUI form controls with their supported label, error, helper-text, required, and
  `autocomplete` APIs. Follow *Accessibility (WCAG 2.2 Level AA)* for label association,
  error identification, and error-summary requirements instead of restating them here.
- Prevent duplicate non-idempotent submissions and represent pending, validation,
  success, and recoverable failure states explicitly.
- Preserve user input after recoverable submission failures unless clearing it is an
  explicit part of the interaction.
- Use `useActionState`, form actions, `useFormStatus`, and `useOptimistic` only when
  the installed React version and hosting framework support the required action model.
- Treat client-side validation as feedback, not as an authorization or authoritative
  business-rule boundary.

## Async Data and Suspense

- Fetch remote data through `src/api/axiosClient.js`, established feature API modules,
  `useApi`, and page-specific Hooks as appropriate. Do not call Axios directly from MUI
  presentation components or introduce a second HTTP-client path.
- Use the data-cache model already selected by the project. Do not introduce a second
  cache for the same remote data or assume Next.js cache and revalidation semantics.
- Define request ownership, cache keys, cancellation, deduplication, freshness, retry,
  invalidation, and mutation reconciliation through the installed library's React APIs.
- Render explicit MUI loading, error, empty, and success states for every affected async
  flow. Prevent stale or contradictory data from remaining actionable while a required
  request is pending or has failed.
- Avoid request waterfalls. Start independent operations together when their ordering
  and failure behavior allow it.
- Prevent an older response from replacing newer state after route, filter, or input
  changes.
- Use `Suspense` only for a data source or lazy component that integrates with
  Suspense. Do not throw ordinary promises or wrap unsupported effect-based fetching in
  Suspense to imitate integration.
- Place Suspense boundaries around independently useful regions and provide stable,
  context-appropriate fallbacks that do not replace more UI than necessary.
- Use error boundaries around async rendering regions where recovery or isolation is
  useful. Keep expected request failures in the data library's established result model.

## React 19 Features

Apply this section only when React 19 or a compatible later version is installed.

- Use `useTransition` for non-urgent state updates whose rendering may interrupt
  high-priority interaction. Do not use a transition to control text inputs.
- Use `useDeferredValue` when an expensive consumer may lag behind a rapidly changing
  value while the current value remains visible elsewhere.
- Use `useOptimistic` only for mutations with a clear authoritative result and rollback
  or reconciliation behavior.
- Use `useActionState` for action result and pending state only when the application
  runtime supports React actions and the pattern fits the established request flow.
- Use `useFormStatus` from a component rendered inside the relevant form. Do not expect
  it to observe a parent or unrelated form.
- Use `useId` for stable accessibility relationships, not for list keys or persisted
  identifiers.
- Adopt React Compiler only when it is configured and supported by the repository.
  Follow its lint rules and compatibility requirements instead of mixing speculative
  compiler assumptions with manual memoization.

## Errors and Boundaries

- Use an established error boundary to isolate unexpected render, lifecycle, and lazy
  loading failures at a scope where useful recovery is possible.
- Do not expect an error boundary to catch event-handler errors, arbitrary asynchronous
  callbacks, rejected requests, or errors thrown by the boundary itself.
- Reset a boundary only when its owning route, resource identity, or explicit retry
  action changes. Avoid infinite retry loops.
- Translate expected API, validation, authentication, authorization, and conflict
  outcomes into typed UI states rather than throwing them as rendering defects.
- Use the established Axios and React error-mapping patterns for safe user feedback. Do
  not duplicate response normalization or error interpretation across components and
  Hooks.
- Report unexpected rendering errors through the project's established React
  error-reporting integration.

## Routing Integration

- Follow the installed React Router DOM 7 declarative routing APIs and the project's
  established route configuration. Do not migrate to a different React Router mode or
  restructure routing without an explicit requirement.
- Keep application routes centralized in `src/routes/routes.jsx`. Use nested `<Route>`
  elements with the established `AuthLayout`, `DashboardLayout`, `<Outlet>`, and
  `ProtectedRoute` patterns, and preserve the catch-all `NotFoundPage` route.
- Use `Link`, `NavLink`, `Navigate`, and `useNavigate` for internal destinations. Avoid
  raw anchors or `window.location` unless a full document navigation is intentional. A
  link to a destination must render a real `<a href>`; never a click-handler-only `Box`,
  `div`, or `IconButton`.
- Keep route parameters, search parameters, and location state explicit and validated
  before using them to load data, build a request, or choose a redirect.
- Use nested layouts, lazy route loading, `Suspense`, error boundaries, and dedicated
  loading, error, or not-found UI according to the existing React Router 7 patterns.
- Treat `ProtectedRoute`, frontend role checks, hidden controls, and client redirects as
  React user-experience behavior only. Do not treat rendered visibility or client route
  state as proof that an operation is permitted.
- Preserve deep links, browser history, refresh, back/forward navigation,
  pending-navigation feedback, and accessible link semantics.
- React Router does not manage focus, screen-reader announcements, or the document title
  on navigation. When a navigation replaces the main content region, update the title
  and metadata through the shared metadata boundary, ensure the destination exposes one
  meaningful primary heading, and move focus intentionally to the main region or its
  heading. Do not move focus for trivial same-page state changes such as opening a
  filter or switching a tab. See *Accessibility (WCAG 2.2 Level AA)* and *SEO and Search
  Discoverability*.

## MUI Responsive Design

- Build mobile-first with the project's MUI breakpoints, responsive `sx` values, fluid
  containers, flexible `Grid` or `Stack` layouts, and relative sizing. Do not invent
  arbitrary breakpoints or rely on rigid fixed-width page layouts.
- Support representative mobile, tablet, laptop, desktop, and large-desktop widths.
  Adapt information priority, navigation, grids, tables, forms, dialogs, and secondary
  content rather than merely shrinking the desktop layout.
- Prevent horizontal overflow, clipped or overlapping content, unreadable typography,
  distorted media, and controls that become unusable at supported viewport sizes.
- Keep content readable and operable at 200% browser zoom and at a 320 CSS-pixel
  viewport width without loss of content or functionality and without two-dimensional
  scrolling for blocks of text.
- Use correctly sized images, modern formats, `srcset`, `sizes`, lazy loading for
  below-the-fold assets, and deliberate font loading through browser, build-tool, MUI,
  and deployment capabilities. Do not introduce `next/image`, `next/font`, or another
  Next.js-only asset utility. Alternative text and layout-shift rules are in *SEO and
  Search Discoverability*.

## Accessibility (WCAG 2.2 Level AA)

Target WCAG 2.2 Level AA on every screen. Where this file or the existing project is
stricter than AA — for example the ~44x44 CSS-pixel touch-target preference — keep the
stricter rule.

- **Native semantics before ARIA.** Use the correct native element (`button`, `a`,
  `input`, `select`, `textarea`, `fieldset`/`legend`, `dialog`, `table`, headings,
  landmarks) and add ARIA only when no native element expresses the interaction. Do not
  add roles or ARIA state that duplicate or contradict native semantics.
- **MUI specifics.** Preserve MUI's built-in semantics and accessibility behavior. Pass
  `component` to change the rendered element; wire label, helper text, and error state
  through the documented label/error props and the installed version's `slotProps` (or
  `inputProps`) APIs; and never rebuild an accessible MUI primitive (`Button`, `Link`,
  `Checkbox`, `MenuItem`, `IconButton`) as a clickable `Box`.
- **Landmarks and skip link.** Render one `<main>` per page and use `header`, `nav`,
  `aside`, `footer`, and `section`/`article` where they apply. When navigation is
  repeated across pages, provide a keyboard-reachable "Skip to main content" link that
  becomes visible on focus and moves focus to the main region.
- **Headings.** Expose exactly one meaningful page-level heading and a logical,
  non-skipping heading order that reflects document structure, not visual size. With MUI
  `Typography`, set the semantic level with `component` and the size with `variant` so
  appearance and level stay decoupled.
- **Keyboard operability.** Every interactive feature works with the keyboard alone: Tab
  / Shift+Tab to reach it, Enter / Space to activate, Escape to dismiss, and arrow keys
  where the pattern requires them. No mouse-only or hover-only interactions. Follow the
  matching WAI-ARIA Authoring Practices pattern for any custom widget.
- **Focus order and indicator.** Let DOM order determine focus order; do not use
  positive `tabIndex`. Keep a visible focus indicator unless you replace it with an
  equally or more visible one. A focused control must never be fully hidden behind
  sticky headers, drawers, dialogs, or floating controls.
- **Focus on overlays and navigation.** When a dialog closes, return focus to the
  control that opened it. On route navigation that replaces main content, move focus
  deliberately (see *Routing Integration*). Do not shift focus for trivial same-page
  state changes.
- **Accessible names.** Every control has a non-empty accessible name — especially icon
  buttons and close, menu, pagination, search, and upload controls. An icon is not a
  name. Prefer a visible label; use `aria-label` / `aria-labelledby` only when a visible
  label is not feasible. A `Tooltip` is supplementary, never the only name.
- **Forms.** Associate every field with a real label (MUI `label`, native `<label>`, or
  `aria-labelledby`); placeholder text is not a label. Validation errors must name the
  field, say how to fix it, be linked via `aria-describedby`, set `aria-invalid`, and
  not rely on color alone. Use `required` / `aria-required`, the correct input `type`,
  and `autocomplete` for identity and contact fields. Provide an accessible error
  summary when it materially helps recovery on a large form. Do not force users to
  re-enter information already provided earlier in the same flow.
- **Status and dynamic updates.** Announce asynchronous outcomes that are not otherwise
  obvious — save succeeded, request failed, upload finished, results loaded — through an
  appropriate polite live region or the project's established MUI feedback components.
  Reserve assertive announcements for genuinely urgent changes.
- **Loading, empty, and error states.** Give every async state accessible context, not a
  bare spinner, and keep loading, success, empty, and error states understandable to
  screen-reader and keyboard users.
- **Dialogs, menus, drawers, popovers.** Preserve MUI's focus trapping, focus
  restoration, Escape handling, and ARIA wiring; do not disable them without a
  documented requirement. Every dialog has an accessible name and, where needed, a
  description. Nothing behind an open modal is focusable.
- **Tables.** Use real table semantics for tabular data, with `<th>` header cells and
  correct `scope`, plus a caption or adjacent description when context is needed. Do not
  use tables for layout. A responsive stacked or card alternative must keep every value
  paired with its label.
- **Color and contrast.** Meet AA contrast for text, meaningful icons, and UI-state
  boundaries. Never signal error, success, warning, required, selected, or availability
  with color alone — add text, an icon, or another cue. Every supported theme, including
  dark mode, keeps AA contrast.
- **Motion.** Honor `prefers-reduced-motion` by removing or reducing non-essential
  transitions and parallax. No essential information is conveyed only through animation,
  and animation must not block interaction longer than necessary.
- **Target size and spacing.** Provide approximately 44x44 CSS pixels for buttons, icon
  controls, navigation actions, pagination, close buttons, checkboxes, and other compact
  or frequently used actions where practical, and never below the WCAG 2.2 target-size
  minimum and spacing allowance.
- **Dragging.** When an interaction uses dragging, provide a single-pointer alternative
  (buttons, menu actions, or click-to-move) unless dragging is essential.
- **Media.** Meaningful video and audio ship the alternatives the content requires —
  captions, transcript, and audio description where applicable. Do not autoplay media
  with sound without an explicit requirement.
- **Images and icons.** Informative images get descriptive `alt`; decorative images get
  `alt=""`; never omit `alt`. Hide a decorative icon from assistive technology
  (`aria-hidden`) when its control already has an accessible name. See *SEO and Search
  Discoverability* for `alt` content quality.
- **Language.** The document declares a correct `<html lang>`. Mark inline passages in
  another language with `lang` when pronunciation matters.
- **Accessible authentication.** Do not require users to memorize values, solve
  cognitive puzzles, transcribe data between steps, or disable password managers or
  paste in order to authenticate. Implement security requirements in the most accessible
  way the security model allows.
- **Consistent help and navigation.** Keep repeated navigation and any help mechanism in
  a consistent order and location across pages.

## SEO and Search Discoverability

This is a client-rendered app. Do not assume CSR provides the SEO guarantees of static
HTML, prerendering, or SSR, and do not add SSR or another rendering framework as part of
SEO work. Implement the controls below within the current architecture. If a
content-heavy public page later needs stronger SEO and CSR proves a measured limitation,
document that architectural limitation rather than silently changing the rendering
model.

- **One metadata boundary.** Manage `<title>`, `<meta name="description">`, canonical,
  `robots`, and social tags through a single shared route/page metadata mechanism. Reuse
  the project's existing solution if one is present; otherwise add one small centralized
  hook or component — not a new document-head library, and not scattered `document.head`
  or `document.title` mutations in feature components.
- **Route classification.** Classify every route as public/indexable, authenticated,
  admin, account/profile, or transient/error. Spend SEO effort only on genuinely public
  content. Emit `<meta name="robots" content="noindex">` for non-public routes. Never
  use `robots.txt` as access control; authentication and authorization stay backend
  responsibilities.
- **Page titles.** Every indexable route sets a unique, descriptive, route-specific
  title that matches its primary heading intent. Avoid generic titles (`Home`, `Page`,
  `Dashboard`) when something more useful exists, avoid keyword stuffing, avoid titles
  duplicated across public pages, and keep a consistent, minimal brand suffix.
- **Meta descriptions.** Give important indexable routes an accurate, user-written,
  unique-where-practical description that summarizes the page and is not misleading. Do
  not invent descriptions for authenticated screens that should not be indexed.
- **Canonical URLs.** For a public page reachable at multiple equivalent URLs, emit
  exactly one canonical pointing to that page's own true URL. Do not add conflicting
  canonicals, point a canonical at the homepage or an unrelated page, or build canonical
  URLs from transient or tracking query parameters.
- **URL design.** Keep public URLs stable, descriptive, predictable, and human-readable.
  Do not put transient UI state in permanent URLs unless it must be shareable or
  bookmarkable; treat query parameters deliberately; do not create multiple indexable
  URLs with identical content without a canonical strategy.
- **Crawlable navigation.** Internal destination links use React Router `Link` /
  `NavLink` and render real `<a href>` markup (see *Routing Integration*). Do not build
  navigation from click handlers on `Box` / `div`, and do not use `window.location` for
  ordinary internal navigation.
- **Semantic structure.** Meaningful HTML and heading hierarchy are required for SEO as
  well as accessibility — follow *Accessibility (WCAG 2.2 Level AA)*. Do not substitute
  `Box` / `div` / `span` where a semantic element is required.
- **Meaningful DOM content.** Important content exists as real rendered text in the DOM.
  Do not place primary content only in CSS-generated content, images, canvas,
  inaccessible custom widgets, or client state that never renders as text. No hidden
  text or other manipulative techniques.
- **Images.** Give informative images descriptive `alt` that neither repeats nearby text
  nor stuffs keywords, and decorative images `alt=""`. Use meaningful file names where
  practical, reserve space with width/height or `aspect-ratio` to avoid layout shift,
  use `srcset` / `sizes` where appropriate, and lazy-load only below-the-fold images —
  never the LCP or above-the-fold image.
- **Structured data.** Add JSON-LD only when it describes visible page content and maps
  to a valid Schema.org type that Google supports. Use correct types and accurate
  values; never fabricate ratings, reviews, prices, or authors; do not add markup
  unrelated to the visible page. Validate with the Rich Results Test before release and
  inject it through the metadata boundary.
- **robots.txt and sitemap.** For the public site, maintain a valid `robots.txt` and,
  where useful, an XML sitemap that contains only canonical public URLs mapping to real
  deployable routes. Keep authenticated, admin, and account pages out of the sitemap.
- **Social metadata.** For pages meant to be shared, provide Open Graph (and a Twitter
  card where needed): title, description, preview image, URL, and type — managed through
  the same metadata boundary and kept separate from indexing and `robots` logic.

## Rendering and Performance

- When React Compiler is enabled, prefer compiler-compatible component code and remove
  redundant manual memoization only within the requested scope and after verification.
- Keep props and context values narrow so unrelated changes do not rerender large
  subtrees.
- Use `lazy` with Suspense for sufficiently heavy components or route modules that do
  not need to be in the initial bundle.
- Use `useTransition` or `useDeferredValue` for expensive non-urgent rendering when
  supported, while keeping direct input updates urgent.
- Virtualize or paginate large collections when rendering every item causes measurable
  responsiveness or memory problems.
- Avoid unnecessary component state, effect-driven render loops, context-wide updates,
  unstable keys, inline component definitions, and repeated expensive work during
  render.
- Use the React Profiler and browser performance tools when the cause of a rendering
  problem is not evident.

## TypeScript with React

- Use discriminated unions for state machines, reducer actions, and variant component
  props that must prevent invalid combinations.
- Prefer `ComponentProps`, `ComponentPropsWithoutRef`, and precise intrinsic-element
  types when wrapping native elements or established components.
- Avoid `React.FC` when it obscures the intended `children` contract or generic props;
  follow the repository's existing convention where it remains clear.

## Client Boundary and Security

- Treat all React client code, environment values, browser storage, network requests,
  and hidden UI as observable and modifiable by users.
- Render untrusted text through JSX's normal escaped interpolation. Do not use
  `dangerouslySetInnerHTML` with untrusted content; sanitize explicitly required rich
  content with the project's proven sanitizer.
- Treat every `VITE_*` value and every other value exposed to React code as public. Keep
  privileged values out of the client bundle and browser storage.

## React Verification

- Exercise changed components through their relevant prop variants, state transitions,
  events, forms, loading, empty, error, success, retry, and unmount paths.
- Verify changed screens at representative MUI mobile, tablet, laptop, desktop, and
  large-desktop breakpoints, including keyboard navigation, visible focus, dialogs,
  menus, form errors, and touch-target behavior.
- Verify Hook ordering, effect dependencies and cleanup, state ownership, list keys,
  context update scope, controlled inputs, and error-boundary behavior.
- Verify routes through direct navigation, in-app navigation, refresh, parameters,
  search values, and back/forward navigation when routing changes.
- Check for unexpected remounts, render loops, duplicate requests, stale async results,
  broad context rerenders, and unnecessary bundle growth.
- Confirm requests still flow through `axiosClient.js` and the established feature API
  modules, and verify `ProtectedRoute` and hidden MUI controls behave consistently with
  the React authentication state.
- Use the production build to verify behavior that development Strict Mode, hot reload,
  compiler transforms, or source maps may affect differently.

## Accessibility and SEO Verification

Run these checks in addition to *React Verification* for any page touched by an
accessibility or SEO change.

- Keyboard and focus: complete the page with the keyboard alone — every control
  reachable and operable, logical focus order, visible focus, no keyboard trap, focus
  never fully obscured, dialogs trap and restore focus, Escape works.
- Semantics: one primary heading with a logical heading order, correct landmarks, real
  link / button / table / form semantics, accessible names on all controls, form labels
  with programmatically associated errors, and error signalling that does not rely on
  color alone.
- Perception: AA color contrast in every supported theme, `prefers-reduced-motion`
  respected, target size and spacing on touch controls, content usable at 200% zoom and
  a 320 CSS-pixel width, and live-region announcements for async outcomes.
- Accessibility tools: run axe (browser extension, or `@axe-core` when already a project
  dependency) and the Lighthouse accessibility audit, inspect the browser accessibility
  tree, and keep `eslint-plugin-jsx-a11y` (or the project's configured a11y lint) green
  by fixing semantics rather than disabling rules. Automated results do not replace the
  manual keyboard and screen-reader-semantics pass.
- SEO for indexable routes: unique `<title>`, useful meta description, correct
  self-referential canonical, expected `robots` / `noindex`, crawlable `<a href>` links,
  meaningful rendered DOM text, image `alt`, valid JSON-LD (Google Rich Results Test),
  and sitemap / `robots.txt` alignment. Confirm no authenticated, admin, or account
  route is indexable.
- SEO tools: the Lighthouse SEO audit for regressions and, in deployed environments,
  Search Console URL Inspection. Do not treat a Lighthouse score alone as proof of
  correct SEO or accessibility.
