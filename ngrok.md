# Local deployment with ngrok

Run the whole stack on your own machine and expose it through ngrok instead of Railway / Azure.

## What you're running

Three local processes, with ngrok in front:

| Piece | Local address | Notes |
|---|---|---|
| PostgreSQL | `localhost:5433` (Docker) | compose maps host **5433** -> container 5432 |
| Backend (`Kahoot.Api`, .NET 10) | `http://localhost:5048` | auto-applies EF migrations + seeds the host user on startup |
| Frontend (Vite build, static) | `http://localhost:3000` | `VITE_API_URL` / `VITE_SIGNALR_URL` are **baked in at build time** |

Required tooling: .NET SDK 10, Node, Docker (for Postgres), ngrok.

## Repo-specific gotchas (read once)

1. **Frontend env is compile-time.** `frontend/src/api/axiosClient.ts` and `frontend/src/realtime/gameHub.ts` read `import.meta.env.*`, which Vite inlines during `npm run build`. Changing the backend URL means rebuilding the frontend.
2. **CORS needs the exact frontend origin.** `backend/src/Kahoot.Api/Program.cs` uses `WithOrigins(...).AllowCredentials()`, so wildcards are rejected. The value must be the frontend's `https://...ngrok-free.app` origin with **no trailing slash and no path**.
3. **ngrok free = one agent session.** Two separate `ngrok http` commands fail with `ERR_NGROK_108`. Two tunnels must come from one `ngrok start --all` using a config file.
4. **ngrok free = one stable domain + up to 3 concurrent tunnels.** Put the **backend** on the stable domain so the frontend build never changes; the frontend tunnel gets a random URL each run.
5. **Interstitial page.** On the free tier, the first full-page visit to any `*.ngrok-free.app` URL shows a "Visit Site" warning (one click, cookie lasts 7 days). It does **not** affect `fetch`/XHR/WebSocket, so no code change is normally needed. Every player's phone sees it once.
6. **HTTPS redirect is fine behind ngrok.** `Program.cs` configures `ForwardedHeaders` trusting any proxy, so ngrok's `X-Forwarded-Proto: https` prevents a redirect loop.
7. **Host login after seeding:** username `IEEEXtreme Section Lead`, password `IEEEXtreme 20.0` (from `backend/src/Kahoot.Api/appsettings.json`).

---

## Path A - two ngrok tunnels

### 1. Configure the ngrok agent

Add your authtoken once (writes it into the ngrok config):

```powershell
ngrok config add-authtoken <YOUR_AUTHTOKEN>
```

Grab your free static domain from the ngrok dashboard -> **Domains** (it looks like `your-name.ngrok-free.app`).

Create a dedicated config file, e.g. `ngrok-kahoot.yml` in the repo root:

```yaml
version: "2"
tunnels:
  backend:
    proto: http
    addr: 5048
    domain: your-name.ngrok-free.app   # your one free static domain
  frontend:
    proto: http
    addr: 3000
    # no domain -> random https URL each run
```

### 2. Start PostgreSQL

```powershell
docker compose up -d db
```

### 3. Start both tunnels

```powershell
ngrok start --all --config ngrok-kahoot.yml
```

Leave it running. Read the two public URLs from the ngrok terminal UI, or:

```powershell
curl http://localhost:4040/api/tunnels
```

- `BACKEND_URL`  = `https://your-name.ngrok-free.app` (stable)
- `FRONTEND_URL` = `https://<random>.ngrok-free.app` (changes every run)

The tunnels return 502 until the local servers are up - that's expected.

### 4. Build and serve the frontend

Create `frontend/.env.production` (Vite auto-loads it for `vite build`; contains no secrets):

```
VITE_API_URL=https://your-name.ngrok-free.app/api
VITE_SIGNALR_URL=https://your-name.ngrok-free.app
```

Then:

```powershell
cd frontend
npm ci
npm run build
npx serve -s dist -l 3000
```

`-s` makes it serve `index.html` for unknown paths, which react-router deep links (`/play/:gameId`, `/host/game/:gameId`) need. Do not use `npm run dev` / `vite preview` behind ngrok - Vite 8 blocks unknown hosts with "Blocked request. This host is not allowed" unless you add `.ngrok-free.app` to `server.allowedHosts` / `preview.allowedHosts` in `vite.config.ts`.

### 5. Start the backend

New terminal, from the repo root:

```powershell
$env:ASPNETCORE_ENVIRONMENT   = "Production"
$env:ASPNETCORE_URLS          = "http://localhost:5048"
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5433;Database=kahoot;Username=postgres;Password=postgres"
$env:Cors__AllowedOrigins__0  = "https://<random>.ngrok-free.app"   # FRONTEND_URL from step 3, no trailing slash
$env:Jwt__SigningKey          = "<a real random secret, 32+ chars>"
dotnet run --project backend\src\Kahoot.Api
```

Watch for `Database migrations applied.` and `Bootstrap host '...' created.` on first run. Windows may prompt to allow `dotnet` through the firewall - allow it for private networks.

`ASPNETCORE_ENVIRONMENT=Production` turns off the Scalar API docs and turns on HSTS. Use `Development` instead if you want `/scalar/v1`; migrations and seeding run either way.

### 6. Open it

Browse to `FRONTEND_URL`, click **Visit Site** on the ngrok interstitial once. Log in with the seeded host credentials. Players join at `FRONTEND_URL` too.

### Per-session checklist

Because the frontend tunnel URL is random, each time you restart ngrok:

1. Note the new `FRONTEND_URL`.
2. Update `$env:Cors__AllowedOrigins__0` and restart **only the backend** (`dotnet run` again - a few seconds).

The frontend build never changes (backend is on the stable domain). To eliminate this churn entirely, either buy a second reserved domain (paid), or use Path B.

---

## Path B - one tunnel, no CORS, nothing changes between sessions

Put a reverse proxy in front so the browser sees a single origin. Using Docker:

**`proxy.conf`** (repo root):

```nginx
server {
    listen 8080;

    location /api/     { proxy_pass http://host.docker.internal:5048; proxy_set_header Host $host; proxy_set_header X-Forwarded-Proto https; }
    location /uploads/ { proxy_pass http://host.docker.internal:5048; proxy_set_header Host $host; proxy_set_header X-Forwarded-Proto https; }
    location /hubs/ {
        proxy_pass http://host.docker.internal:5048;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto https;
    }
    location / {
        root /usr/share/nginx/html;
        try_files $uri $uri/ /index.html;
    }
}
```

Build the frontend with **relative** URLs (leave `VITE_SIGNALR_URL` empty so `gameHub.ts` falls back to same-origin):

```
# frontend/.env.production
VITE_API_URL=/api
VITE_SIGNALR_URL=
```

```powershell
cd frontend
npm run build

docker run -d --name kahoot-proxy -p 8080:8080 `
  -v ${PWD}\dist:/usr/share/nginx/html:ro `
  -v ${PWD}\..\proxy.conf:/etc/nginx/conf.d/default.conf:ro `
  nginx:alpine
```

Run the backend as in Path A step 5 but **drop the `Cors__` line** (same origin, so CORS never fires). Then one tunnel on your stable domain:

```powershell
ngrok http 8080 --url your-name.ngrok-free.app
```

Everyone uses that single URL forever; no rebuilds, no CORS edits.

---

## Docker Compose variant

The compose file bakes `VITE_API_URL=http://localhost:5000/api` into the frontend image, which won't work remotely - override it and the backend CORS:

```powershell
docker compose build `
  --build-arg VITE_API_URL=https://your-name.ngrok-free.app/api `
  --build-arg VITE_SIGNALR_URL=https://your-name.ngrok-free.app `
  frontend
```

`docker-compose.override.yml`:

```yaml
services:
  backend:
    environment:
      Cors__AllowedOrigins__2: "https://<random-frontend>.ngrok-free.app"
```

```powershell
docker compose up -d
```

Then two tunnels via `ngrok start --all` pointing at `5000` (backend) and `3000` (frontend) instead of 5048/3000. Same random-URL churn as Path A.

---

## If API calls hit `ERR_NGROK_6024`

Only if you actually see it (you usually won't). Add the bypass header:

```ts
// axiosClient.ts
export const axiosClient = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': 'true' },
  timeout: 10000,
});
```

```ts
// gameHub.ts - inside .withUrl(hubUrl, { ... })
headers: { 'ngrok-skip-browser-warning': 'true' },
```

---

## Security notes

- This publishes your dev machine to the public internet. The seeded host password is a known value from `appsettings.json` - change `Seeding__Host__Password` (env var) before exposing it, or don't leave the tunnel up unattended.
- Always set a real `Jwt__SigningKey`; the one in `appsettings.json` is a committed dev placeholder.
- ngrok free caps traffic at 4,000 HTTP requests/min. A live game is mostly one persistent WebSocket per player, so classroom-size sessions are fine.
