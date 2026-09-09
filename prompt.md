# Claude Code Prompt — Finish the Platform: Remaining Phases, Full Compatibility, Zero Comments, Beautiful UI

Paste the fenced block below into **Claude Code** at the repository root. It assumes Phases 0–6 of
`docs/frontend-pages-plan.md` are complete (see `.wolf/STATUS.md`) and drives the project to a
finished, internally consistent, Docker-runnable state.

---

```markdown
You are a Principal Full-Stack Engineer (ASP.NET Core + SignalR + EF Core) and a Principal
Frontend Engineer / UI-UX specialist, finishing the IEEEXtreme Kahoot-like real-time quiz platform.

Phases 0–6 of `docs/frontend-pages-plan.md` are done (`.wolf/STATUS.md`). Your job is to complete
everything that remains, make the backend and frontend provably compatible, close every gap against
the requirement docs, strip all comments, and make the UI genuinely beautiful — while keeping the
whole system runnable with a single `docker compose up --build`.

**HARD CONSTRAINT — no test files anywhere.** Do NOT write, generate, or scaffold any testing file
in this repository — no unit tests, no integration tests, no concurrency tests, no SignalR tests,
no test projects, no test fixtures/mocks, and no new load-test files. Do not add a testing
framework or testing dependency. Where `docs/Kahoot-like-Platform.md` asks for tests, treat that as
explicitly out of scope for this pass; note it as a known limitation instead. Production code must
still be written so it *could* be tested later (isolated business logic, explicit dependencies,
deterministic behaviour), and any tests that already exist must keep passing.

## 0. Binding standards (do NOT restate them — follow them)

These files govern this work. Read the ones relevant to each change before you touch code:

- `CLAUDE.md` — project-wide engineering standards (clean code, DRY, SoC, SOLID, KISS, YAGNI,
  security, performance, error handling, contracts, version-control hygiene, "ask when
  contradictory/ambiguous"). This is the definition of "clean code" for this task.
- `frontend/AGENTS.md` — React / MUI / React Router 7 / Axios / WCAG 2.2 AA / SEO / responsive /
  performance / TypeScript rules for the client. Treat every rule as a hard requirement.
- `backend/AGENTS.md` — C# / Clean Architecture / EF Core / migration rules (incl. "no comments in
  C# code"; every model change updates `backend/projectSchema.dbml` + a NEW migration in the same
  task; migrations are immutable).
- `docs/functional-requirements.md`, `docs/non-functional-requirements.md` — the living spec. Keep
  them synchronized with any behavioural change you make.
- `docs/Kahoot-like-Platform.md` — the original brief (acceptance criteria §35, definition of done
  §37, required artifacts).
- `docs/realtime-protocol.md` — the typed SignalR contract. Keep it in sync with the hub.
- `docs/frontend-pages-plan.md` — the page/phase roadmap; Phase 7 (lines 683–733) is the remaining
  frontend phase.
- `.wolf/` — OpenWolf state. Follow `.claude/rules/openwolf.md`: `openwolf find` before locating
  symbols; check `.wolf/cerebrum.md` "Do-Not-Repeat" before generating; `openwolf bug search` before
  fixing a bug and log it after; never hand-edit `anatomy.md` / `memory.md`.

Work phase by phase. After each phase, run the verification gates in §7 and fix everything before
moving on. Do not declare a phase done while a known failure remains in its scope. When something is
missing, ambiguous, or contradictory (see §8), stop and ask before assuming.

## 1. Frontend Phase 7 — Resilience, Errors, Accessibility, Performance, SEO, Polish

Implement `docs/frontend-pages-plan.md` Phase 7 in full. No new pages — hardening only.

1. **One global HTTP-error policy** in the Axios boundary (`src/api/axiosClient.ts` + the shared
   `ApiError`): `401` clears tokens, redirects to `/login` preserving the return URL, and shows a
   "session expired" notice; `403` → forbidden state with a route back; `404` → not-found;
   `409` → reconcile from the server broadcast/echo, never a stuck spinner; `429` → cooldown copy,
   disable the action, no retry storm; `5xx` → `ErrorState` + Retry. One place, not per page.
   Do not duplicate error normalization across hooks/components.
2. **Connection resilience** for both `/play/:gameId` and `/host/game/:gameId`: handle
   `onreconnecting` / `onreconnected` / `onclose` with backoff + jitter; on reconnect always
   re-invoke `Reconnect` (player) or `JoinAsHost` + `GET /games/{id}` (host) and fully re-hydrate;
   freeze all countdowns while disconnected (the `paused` path already exists in
   `useServerCountdown`); the `disconnected` state is a blocking `ConnectionStatusBanner` overlay
   with a manual Retry that keeps the last known view underneath; if the game no longer exists on
   reconnect, fall back to Finished / Not-found rather than looping. Cover backend restart and a
   near-simultaneous mass reconnect (spread `Reconnect` calls with jitter).
3. **Idempotency UX**: audit that every host control and the player submit are debounced with a
   synchronous in-flight guard and reconciled against the broadcast echo (this exists from Phase 5
   via `actionInFlightRef` / `submitInFlightRef` — verify and finish). A double-tap must never
   produce a double effect in the UI.
4. **Accessibility (WCAG 2.2 AA) across every screen** per `frontend/AGENTS.md`:
   - Exactly one meaningful `<h1>` per phase/view; logical, non-skipping heading order; correct
     landmarks (`<main>`, `header`, `nav`, `footer`); a keyboard-reachable "Skip to main content"
     link where nav repeats.
   - Full keyboard path through every choice tile, host control, dialog, menu, and form; visible
     focus indicators never hidden behind sticky bars/overlays; DOM-order focus, no positive
     `tabIndex`.
   - On a route/phase change that replaces the main region, move focus to the phase heading and
     update the title/metadata through the single metadata boundary; do NOT move focus for trivial
     same-page state changes.
   - Wire the existing `LiveRegion` (`src/components/Feedback/LiveRegion.tsx`, currently unused)
     through `usePlayerGame` / `useHostGame` transitions for polite announcements: "question
     started", "answer accepted", "results are in", "player removed", "you were removed",
     "leaderboard updated", "reconnecting", "reconnected". Reserve assertive only for genuinely
     urgent changes.
   - Non-colour cues on every choice / result / status / rank-delta (shape + letter + icon +
     text — mostly present; audit). AA contrast in the light theme for text, meaningful icons, and
     state boundaries.
   - `prefers-reduced-motion` removes/*reduces* every non-essential animation (pulse rings, points
     pop, bar transitions, podium) — audit that each animated block already guards it.
   - Forms: real associated labels, `aria-invalid` + `aria-describedby` on errors, error text that
     names the field and how to fix it, `inputmode="numeric"` + sensible `autocomplete` on PIN,
     `autocomplete` on nickname and login; never block paste on login.
   - ~44×44 CSS-px targets for buttons, icon buttons, close/menu/pagination/kick controls.
   - Give every async state accessible context (never a bare spinner).
   - Add per-route `AppErrorBoundary` around `/play/*` and `/host/game/*` that resets on the
     `gameId` change (pages are already keyed by `gameId`); translate expected API/validation/
     auth/conflict outcomes into typed UI states, not thrown render errors.
5. **Responsive / display**: `/host/game/*` projector density — legible across a room, and confirm
   the host-only correct-answer highlight is clearly separated and can never appear on a
   player screen. `/play/*` mobile density — usable at a 320 CSS-px width and 200% zoom, with
   safe-area insets, one-handed, no horizontal overflow. Laptop fallback for the host. Verify at
   representative mobile / tablet / laptop / desktop / large-desktop widths.
6. **Performance (NFR-1, 500 participants)**: the lobby participant grid and the results rendering
   must stay responsive at 500 — windowed/virtualised or a capped visible set + search + "and N
   more" (no new dependency without asking); batched participant events (host already batches at
   150 ms — verify); memoised rows; a keyed `Map`, never a full re-sort per event; no request
   waterfalls; `Reconnect` storms spread with jitter. Keep props/context narrow; use `lazy` +
   `Suspense` for route modules heavy enough to matter; do not add memoization/parallelism without
   a measured reason.
7. **SEO** (client-rendered — do not add SSR): one metadata boundary (`MetadataManager`) owns
   `<title>` / description / canonical / robots / Open Graph. `/`, `/join`, `/login` get unique,
   descriptive, route-specific titles + accurate descriptions + self-referential canonicals + Open
   Graph tags; every host/play route stays `noindex`. Add `frontend/public/robots.txt` and a
   minimal `frontend/public/sitemap.xml` covering only `/`, `/join`, `/login`. Add a real favicon
   and an OG preview image derived from the existing brand asset (no copyrighted Kahoot assets).
   All internal navigation renders real `<a href>` via React Router `Link`/`NavLink` — no
   click-handler-only `Box`/`div`, no `window.location` for in-app navigation.

## 2. Whole-app completion and gap-fill

After Phase 7, reconcile the entire codebase against `docs/functional-requirements.md`,
`docs/non-functional-requirements.md`, and `docs/Kahoot-like-Platform.md`:

- Produce a written **gap analysis** (a short section in `.wolf/STATUS.md` or a scratch note you
  show me): for every FR and every acceptance-criterion in the brief §35, state
  implemented / partial / missing, with the file(s) that satisfy it.
- Implement the **production-code** gaps you find (features, endpoints, DTO fields, validation,
  authorization, rate limiting, reconnection, state-machine guards, isolation, health, structured
  logging, config).
- Confirm the required docs exist and are current: create `docs/architecture.md` (deployment,
  HTTP, SignalR, answer-submission, and game-state-machine Mermaid diagrams, each explained) and
  `docs/final-report.md` (architecture, technology decisions, DB schema/constraints/indexes,
  real-time protocol, concurrency strategy, load-test results **only if actually run — never
  fabricate**, deployment + env vars, known limitations, scaling recommendation). Keep
  `docs/realtime-protocol.md` in sync with the hub.
- Verify there are **no missing files** implied by the requirements: entry points, route
  registrations, index barrels, feature API modules, realtime typed contracts, constants,
  migrations for any model change, `frontend/.env.example` + backend env-var documentation,
  `robots.txt` / `sitemap.xml`, Dockerfiles, `nginx.conf`, health endpoint.
- Per the HARD CONSTRAINT above: create NO test files of any kind and add NO testing dependency.
  Still design all production code to be testable (business logic isolated from transport and I/O,
  explicit dependencies, deterministic behaviour). List "automated tests / load tests" in the gap
  analysis as an explicit out-of-scope limitation, not as work to do.

## 3. Backend ↔ Frontend compatibility (make it provable, not assumed)

Audit and fix every mismatch between what the backend sends and what the frontend expects:

- **REST**: for every endpoint the frontend calls (`src/api/*`), confirm the route, method,
  request body shape, success DTO field names, and status codes match the controllers. The
  frontend must read `problem+json` `title` / `detail` and the `code` extension consistently
  through `ApiError` — no per-call parsing.
- **Enums on the wire**: `GameStatus` is serialized as an **integer** (no `JsonStringEnumConverter`
  is configured). Every place the frontend consumes a status (REST and SignalR) must go through
  `normalizeGameStatus`. Verify no code compares a raw status to a string literal without
  normalizing.
- **SignalR**: default JSON hub protocol serializes payloads **camelCase**. For every `IGameClient`
  event and every hub method (`JoinGame`, `Reconnect`, `SubmitAnswer`, `JoinAsHost`), confirm the
  frontend's TypeScript payload types in `src/realtime/events.ts` exactly match the C# records in
  `Kahoot.Application.Games.Common`, including nullability. Confirm the `RealtimeResponse<T>`
  envelope (`success` / `data` / `error.{code,description}`) is handled everywhere and the hub
  never throws.
- **Group isolation**: players receive `game:{gameId}` events, host receives `game:{gameId}:host`;
  the correct-answer payload (`QuestionStartedForHost` / `correctChoiceId`) must never reach a
  player connection. Verify.
- **Error codes**: every `Game.*` / `Quiz.*` / `Auth.*` code the backend can return has friendly
  copy in `src/constants/errorCodes.ts` and the frontend maps it to the right UX (field error vs
  banner vs redirect vs kick).
- **CORS**: the backend allows the frontend origin(s) it will actually be served from in Docker
  (`http://localhost:3000`) and in dev (`http://localhost:5173`).
- **Reconnection contract (FR-7)**: `Reconnect` restores identity, game, state, current question,
  deadline, already-answered, score, rank; results during `QuestionResults`/`Leaderboard`;
  leaderboard during `QuestionResults`/`Leaderboard`/`Finished`. The frontend consumes all of it.
- Fix mismatches at the correct layer (usually the frontend types/mapping; touch the backend only
  when the backend is actually wrong, and then update `projectSchema.dbml` + a new migration +
  `realtime-protocol.md` as required by `backend/AGENTS.md`).

## 4. Zero comments — remove every comment from the code

- No `//`, no `/* … */`, no JSDoc `/** … */`, no JSX `{/* … */}`, and no C# `//` or `///`
  XML-doc comments anywhere in `frontend/src` **or** `backend/src`.
- Remove existing comments in any file, not only files you otherwise touch. Where a comment carried
  real intent, encode it in a clear name, a small well-named function, or a named constant instead.
- The only tolerated `//` / `/*` sequences are inside string literals, URLs, and regular
  expressions. Preserve non-comment annotations that are not comments (C# attributes, DBML `Note:`
  blocks, `#pragma`, JSON, YAML).
- Before finishing, prove compliance:
  - `rg -n --pcre2 '(^|[^:])//(?!/)|/\*|\{/\*|///' frontend/src` returns nothing but string/URL/regex hits.
  - `rg -n --pcre2 '//|/\*|///' backend/src` returns nothing but string/URL/regex hits.

## 5. Clean code, DRY, separation of concerns, performance, accessibility, security

Apply `CLAUDE.md` and `frontend/AGENTS.md` as the standard. Specifically for this pass:

- **DRY**: one leaderboard renderer, one countdown source, one media-URL resolver, one error
  mapper, one metadata boundary, one live-region announcer, one status normalizer. Delete parallel
  or dead implementations you find; verify no references first.
- **Separation of concerns**: no Axios calls or response shaping in MUI presentation components;
  business/orchestration logic in hooks and `src/api` / `src/realtime`, not in JSX; the SignalR hub
  stays transport-only and delegates to `Kahoot.Application` use cases; keep the dependency
  direction clean.
- **Performance**: no N+1 EF queries or full-graph `Include` on hot paths; `AsNoTracking` +
  projections for reads; short transactions; the answer hot path stays minimal; do not broadcast
  the full leaderboard per answer; on the client, no render loops, no unstable keys, no
  effect-driven state chains, narrow props/context.
- **Accessibility**: as in §1.4 — treat it as correctness, not decoration.
- **Security**: server is authoritative for correctness, timing, scoring, state, and the correct
  answer; never expose the correct answer to players before reveal; host-only operations require a
  valid access token and game ownership; validate/sanitize PIN and nickname; keep rate limits
  player-friendly but real; never log secrets/tokens; every `VITE_*` value is public — keep
  privileged values out of the bundle and browser storage; escaped JSX for untrusted text, no
  `dangerouslySetInnerHTML` with untrusted content; secrets only via configuration/env.

## 6. UI/UX — make it creative, attractive, and genuinely beautiful (highest priority)

Within the guardrails (`frontend/AGENTS.md`: MUI-only, strictly light theme, IEEE palette via
theme tokens not hardcoded hex, React Router 7, no new UI/animation dependency without asking):

- **A single, cohesive design language** end to end — spacing scale, radius, elevation, type
  scale, iconography, and motion feel like one product. Extend the MUI theme for reusable tokens
  and variants instead of ad-hoc `sx` divergence.
- **Palette**: Primary IEEE Ocean Blue `#00629B`, Secondary/Accent Radar Cyan `#0284C7`, canvas
  `#F4F8FC`, surface `#FFFFFF`, text `#09131F` / `#486581` / `#64748B`, border `#E2E8F0`,
  Success `#10B981`, Warning `#F59E0B`, Error `#EF4444`. Use tokens; keep AA contrast.
- **Motion with intent**: 150–250 ms cubic-bezier transitions; a smooth pulsing "waiting" state;
  a satisfying answer-lock and points count-up; a leaderboard/podium reveal that feels like a live
  event — all disabled or reduced under `prefers-reduced-motion`, never load-bearing for meaning.
- **Every state is designed**: loading (skeletons for known layout, never a bare spinner), empty
  ("no players yet — share the PIN", "no quizzes — create your first"), error, disconnected/
  reconnecting, kicked, finished. These should look intentional and on-brand, not like fallbacks.
- **Two distinct, polished experiences**: the host/projector screens read across a room and feel
  like hosting a live show (huge PIN, bold timer, confident controls, clear phase indicator);
  the player screens feel tactile and fun in one hand on a phone (big thumb-friendly answer tiles
  with shape + letter identity, immediate feedback, a clear "your rank" hero).
- **Landing, Join, and Login** should be inviting and modern — a strong hero, obvious primary
  action, and a short, honest explanation of the product.
- Kahoot-inspired **interaction model only** — original branding, colours, copy, shapes, and
  layout. No copyrighted Kahoot assets or trade dress.
- Keep it fast and accessible: creative motion and layout must not regress LCP, keyboard
  operability, contrast, or the 320 px / 200% zoom requirement.

## 7. Verification gates (run after every phase and before declaring done)

From `frontend/`:
- `npm run format:check` — clean
- `npm run lint` — 0 errors, 0 warnings (including `react-hooks` v7: `refs` / `purity` /
  `set-state-in-effect` / `static-components`)
- `npm run build` (`tsc -b && vite build`) — clean
- the two ripgrep comment audits from §4

From `backend/`:
- `dotnet build` (or `dotnet build backend/Kahoot.slnx`) — clean, 0 warnings you introduced
- any tests that already exist still pass (add none — see the HARD CONSTRAINT)
- `dotnet ef migrations list` consistent with `projectSchema.dbml` if you changed the model

Docker (from repo root):
- `docker compose build` — all three services build
- `docker compose up -d` — `db` healthy, `backend` reachable on `http://localhost:5000/health`
  (200, no sensitive data), `frontend` served on `http://localhost:3000`
- In a browser against the compose stack: deep-link `http://localhost:3000/join` and
  `http://localhost:3000/play/<id>` load (nginx SPA fallback), the SPA reaches the backend REST +
  SignalR, CORS passes, and a full host→player round works (create game, join, start question,
  answer, results, leaderboard, end). If the frontend image needs `VITE_API_URL` /
  `VITE_SIGNALR_URL` to target `http://localhost:5000`, wire it through compose `build.args` (or a
  documented runtime-config approach) — do not hardcode URLs in application code.

E2E browser check (dev or compose): host lobby → start → player question within a moment →
one answer accepted, duplicate is idempotent → end question → results on both → leaderboard with
the player's rank → next question → end game → Finished/podium on both → player "Play again"
clears the session and returns to `/join`. Reconnection: reload the player tab and force a socket
drop during an active question (before and after answering) — identity, view, deadline, and
answered-state all restore with no duplicate participant on the host.

Report exactly what you ran and what passed. If a check could not run (e.g. no Docker daemon,
backend not buildable in this environment), say so explicitly — do not claim success you did not
observe, and never fabricate load-test numbers.

## 8. Decisions to raise before implementing (per CLAUDE.md "ask when contradictory")

Testing is already decided: **no test files of any kind** (see the HARD CONSTRAINT). Do not ask
about it. `docs/Kahoot-like-Platform.md` §29/§35/§37 test requirements are out of scope for this
pass by explicit instruction — record them as a known limitation.

Ask me these before generating the corresponding artifacts:

1. **Load tests**: a `load-tests/` k6 suite already exists on disk. Do you want it *run* (adding no
   new files) with the real 50→500 numbers recorded in `docs/final-report.md`, or should load
   testing be left untouched and listed as out of scope?
2. **Deployment targets**: the brief targets Vercel (frontend) + Railway (backend). This pass is
   scoped to "runs under `docker compose up`". Should I also add/verify Vercel and Railway config,
   or is Docker Compose the only deployment target for now?
3. **Virtualisation dependency**: if the 500-participant grid needs a windowing library to stay
   smooth, do you approve adding one (e.g. a small `react-window`-class dependency), or must I use
   the no-dependency capped-list + search fallback?

Proceed with everything else without waiting.

## 9. Definition of done

- Frontend Phase 7 fully implemented; every UI-state-catalog row (`docs/frontend-pages-plan.md` §5)
  has a real treatment on every page that can reach it.
- Gap analysis written; every production-code gap against the requirement docs closed or explicitly
  listed as an answered §8 decision.
- Backend and frontend contracts verified compatible (REST + SignalR + enums + error codes + CORS
  + reconnection).
- Zero comments in `frontend/src` and `backend/src`, proven by ripgrep.
- `docs/architecture.md` and `docs/final-report.md` exist and are accurate;
  `functional-requirements.md` / `non-functional-requirements.md` / `realtime-protocol.md` updated
  for any behavioural change.
- All §7 verification gates pass, or the exact reason a gate could not run is documented.
- `docker compose up --build` brings up db + backend + frontend and a full host→player game works
  end to end through the compose stack.
- `.wolf/STATUS.md` updated; a concise final summary of what changed, key decisions, verification
  performed, and remaining limitations.
```

---

*Generated to close out `docs/frontend-pages-plan.md` (Phase 7 + reconciliation) and the
`docs/Kahoot-like-Platform.md` definition of done, aligned with `CLAUDE.md`, `frontend/AGENTS.md`,
and `backend/AGENTS.md`.*
