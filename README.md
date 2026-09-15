# Kahoot-like Real-Time Quiz Platform

A high-concurrency, real-time quiz and trivia application designed for interactive multiplayer competitions. Built with **ASP.NET Core 10**, **SignalR**, **PostgreSQL 18**, and **React 19 (Vite + Material UI)**.

---

## Architecture Overview

The application features a clean separation between **Local Development** and **Production Deployment**:

### Local Development (Direct Connections)
In development, services communicate directly on dedicated ports without an intermediate reverse proxy, preventing port 80 host conflicts (such as with local Apache or IIS servers):

```
┌───────────────────────────────────────┐
│     Vite Dev Server (React 19)        │  http://localhost:5173
└───────┬───────────────────────────────┘
        │
        ├─ REST API: http://localhost:5000/api
        ├─ WebSockets: http://localhost:5000/hubs/game
        │
┌───────▼───────────────────────────────┐
│     ASP.NET Core 10 Web API + Hub     │  http://localhost:5000 (:8080 internal)
└───────┬───────────────────────────────┘
        │
        ├─ PostgreSQL Protocol: port 5433 (or 5432)
        │
┌───────▼───────────────────────────────┐
│     PostgreSQL 18 Database            │  localhost:5433
└───────────────────────────────────────┘
```

### Production Deployment (Azure VM with Nginx Edge Proxy)
In production (`docker-compose.prod.yml`), Nginx acts as the single public entry point on port 80, keeping internal application containers private:

```
                          http://<VM_IP>/ (Port 80)
                                    │
                         ┌──────────▼──────────┐
                         │  Nginx Edge Proxy   │
                         └──────────┬──────────┘
                                    │
          ┌─────────────────────────┼─────────────────────────┐
          │                         │                         │
     / (Root)                    /api/ & /hubs/          /grafana/
          │                         │                         │
┌─────────▼─────────┐     ┌─────────▼─────────┐     ┌─────────▼─────────┐
│ Frontend Container│     │ Backend Container │     │ Grafana Dashboard │
│  (Internal :8080) │     │  (Internal :8080) │     │  (Internal :3000) │
└───────────────────┘     └─────────┬─────────┘     └───────────────────┘
                                    │
                          ┌─────────▼─────────┐
                          │ Database Container│
                          │  (Internal :5432) │
                          └───────────────────┘
```

---

## Quick Start: Local Development

### Prerequisites
- [Docker Desktop](https://www.docker.com/) (or Docker Engine with Compose v2)
- [Node.js 22+](https://nodejs.org/) (for running the frontend Vite dev server)
- [.NET 10 SDK](https://dotnet.microsoft.com/) *(optional, only if running the backend outside Docker)*

---

### Step 1: Clone and Configure Environment Files

1. **Root Environment Variables:**
   Copy `.env.example` to `.env` (or verify `.env` exists):
   ```bash
   cp .env.example .env
   ```
   Key development defaults in `.env`:
   - `BACKEND_PORT=5000`
   - `DB_PORT=5433` (prevents collision with local PostgreSQL on 5432)
   - `FRONTEND_PORT=5173`
   - `CLIENT_BASE_URL=http://localhost:5173`
   - `CORS_ALLOWED_ORIGINS=http://localhost:5173,http://localhost:3000`

2. **Frontend Environment Variables:**
   Copy `frontend/.env.example` to `frontend/.env`:
   ```bash
   cp frontend/.env.example frontend/.env
   ```
   Key development defaults in `frontend/.env`:
   ```env
   VITE_API_URL=http://localhost:5000/api
   VITE_SIGNALR_URL=http://localhost:5000/hubs/game
   ```

---

### Step 2: Start Backend and Database (via Docker Compose)

In development, Docker Compose starts **only** the database and backend API (Nginx and monitoring services remain disabled by default):

```bash
docker compose up -d
```

Verify the services are running:
```bash
docker compose ps
```
You should see:
- `kahoot-db` running on `0.0.0.0:5433->5432/tcp`
- `kahoot-backend` running on `0.0.0.0:5000->8080/tcp`

Verify backend health:
```bash
curl http://localhost:5000/health
```
*(Expected response: `Healthy`)*

---

### Step 3: Start the Frontend Development Server

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server will start on [http://localhost:5173](http://localhost:5173).

---

### Step 4: Access the Application

- **Frontend Web App:** [http://localhost:5173](http://localhost:5173)
- **Host Login:** Click **Host Login** and sign in with the default seeded credentials:
  - **Username:** `IEEEXtreme Section`
  - **Password:** `IEEEXtreme@123456789`
- **Backend API Base:** [http://localhost:5000/api](http://localhost:5000/api)
- **SignalR Game Hub:** `http://localhost:5000/hubs/game`

---

## Alternative: Running the Backend Locally (.NET SDK)

If you prefer debugging the backend using your IDE or `dotnet run` instead of the backend Docker container:

1. Start only the PostgreSQL database:
   ```bash
   docker compose up -d db
   ```
2. Run the API project:
   ```bash
   cd backend/src/Kahoot.Api
   dotnet run --launch-profile http
   ```
   The backend will load `appsettings.Development.json` and connect to `localhost:5433`.

---

## Production Deployment (Azure VM)

To deploy the production stack with Nginx on the Azure VM:

1. Set production environment variables in `.env` using `.env.production.example` as a template:
   ```env
   CLIENT_BASE_URL=http://<VM_IP>
   CORS_ALLOWED_ORIGINS=http://<VM_IP>
   VITE_API_URL=/api
   VITE_SIGNALR_URL=/hubs/game
   ```
2. Launch the production containers:
   ```bash
   docker compose -f docker-compose.prod.yml up -d --build
   ```
3. The platform will be accessible at `http://<VM_IP>/` on port 80.

---

## Optional: Running the Monitoring Stack (Observability)

Grafana, Prometheus, Loki, Jaeger, and system exporters are configured with Docker Compose profiles. They do not run by default. To start them on demand:

```bash
docker compose --profile monitoring up -d
```

- **Grafana Dashboard:** `http://localhost:3001` (or proxied via `http://localhost/grafana/` if Nginx profile is enabled)
- **Prometheus:** `http://localhost:9090`
- **Jaeger Tracing:** `http://localhost:16686`
- **Loki Logs:** `http://localhost:3100`
