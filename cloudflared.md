# Cloudflare Tunnel Setup Guide for Kahoot

This guide documents how to expose the local Kahoot stack to the public internet using **Cloudflare Tunnel (`cloudflared`)** with **unlimited concurrent connections**, full **SignalR / WebSocket** support, and **zero rate-limiting constraints**.

---

## Why Cloudflare Tunnel instead of ngrok?

| Feature | ngrok (Free Plan) | Cloudflare Tunnel (Free) |
| :--- | :--- | :--- |
| **Simultaneous Connections** | Strictly limited to **100/min** (drops on 500+ players) | **Unlimited** (handles 500, 1000+ players) |
| **SignalR WebSockets** | Throttled / proxy EOF drops under load | Native, ultra-low latency WebSocket support |
| **DDoS & Bandwidth** | 1 GB monthly limit | Global Anycast network, unlimited bandwidth |
| **Cost** | Requires paid upgrade for 100+ players | **100% Free forever** |
| **Protocol Reliability** | Standard TCP | HTTP/2 over TCP (`--protocol http2`) |

---

## Architecture Overview

```
[ Public Internet: Players / Load Generators ]
                     │
                     ▼ (HTTPS / WSS)
        Cloudflare Edge Network
                     │
                     ▼ (HTTP/2 Encrypted Tunnel via port 443)
           cloudflared.exe (Local)
                     │
                     ▼ (HTTP/1.1 on localhost:3000)
       Nginx Frontend Reverse Proxy (kahoot-frontend)
            ├── /                -> React Single-Page App
            ├── /api/            -> kahoot-backend (ASP.NET Core 10)
            ├── /hubs/game       -> SignalR WebSockets (kahoot-backend)
            └── /health          -> Health check endpoint
```

---

## Prerequisites

1. **Docker Compose running locally**:
   ```powershell
   docker compose up -d
   ```
2. **Verify local health**:
   ```powershell
   curl http://localhost:3000/health
   # Expected output: Healthy
   ```
3. **`cloudflared.exe`**:
   The standalone binary `cloudflared.exe` is already present in the project root directory.

---

## Step-by-Step Instructions

### Step 1: Start the Cloudflare Tunnel

Open a PowerShell terminal in the repository root and run:

```powershell
.\cloudflared.exe tunnel --protocol http2 --url http://localhost:3000
```

> [!TIP]
> **Why `--protocol http2`?**
> By default, `cloudflared` attempts to use UDP (QUIC), which some home routers or Wi-Fi firewalls intermittently throttle or drop, causing DNS timeout warnings. Using `--protocol http2` forces reliable TCP HTTPS connections, guaranteeing 100% rock-solid uptime.

---

### Step 2: Copy the Public URL

Once started, `cloudflared` outputs logs in the terminal. Look for the line:

```text
2026-09-11T... INF +--------------------------------------------------------------------------------------------+
2026-09-11T... INF |  Your quick Tunnel has been created! Visit it at:                                         |
2026-09-11T... INF |  https://xxxx-xxxx-xxxx.trycloudflare.com                                                  |
2026-09-11T... INF +--------------------------------------------------------------------------------------------+
```

Copy your URL (e.g., `https://furnished-impression-juice-recognize.trycloudflare.com`).

---

### Step 3: Access the Application

- **Players / Join Screen**: Open `https://<subdomain>.trycloudflare.com` in any mobile browser or desktop.
- **Host / Admin Dashboard**: Navigate to `/host` or login with host credentials:
  - **Username**: `IEEEXtreme Section`
  - **Password**: `IEEEXtreme@123456789`
- **Health Check**: `https://<subdomain>.trycloudflare.com/health` (returns `Healthy`).

---

## Running Load Tests Against Cloudflare Tunnel

### 1. Update `load-tests/.env`
Whenever you launch a new Quick Tunnel, update lines 3 and 4 in `load-tests/.env`:

```env
BASE_URL=https://<your-subdomain>.trycloudflare.com/api
SIGNALR_URL=https://<your-subdomain>.trycloudflare.com
HUB_PATH=/hubs/game
```

### 2. Execute Tests

#### Option A: 500-Player Simultaneous Answer Burst (Fastest & Highest Stress ~ 1 minute)
Tests 500 concurrent players in the lobby submitting answers in the exact same second:

```powershell
node load-tests/run-all.js answer-burst -- -e PLAYERS=500 -e JOIN_RAMP=30s -e ANSWER_TIME_LIMIT=15 -e SETTLE_SECONDS=10
```

#### Option B: Stress & Breakpoint Test (Ramp ~ 3-4 minutes)
Progressively increases concurrent load (`100 -> 250 -> 500`) to find the breaking point:

```powershell
node load-tests/run-all.js ramp -- -e RAMP_LEVELS="100,250,500" -e RAMP_STEP_RAMP=30s -e RAMP_STEP_HOLD=30s
```

#### Option C: Full Test Suite (~ 45 minutes)
Runs all 8 acceptance scenarios sequentially:

```powershell
node load-tests/run-all.js
```

---

## Troubleshooting

### 1. "lookup region1.v2.argotunnel.com: i/o timeout"
- **Cause**: Router or ISP DNS dropped UDP packets on the default QUIC protocol.
- **Fix**: Always include `--protocol http2` when launching the tunnel:
  ```powershell
  .\cloudflared.exe tunnel --protocol http2 --url http://localhost:3000
  ```

### 2. Backend / Database Restart
If you ever clean or rebuild Docker containers:
```powershell
docker compose down -v
docker compose up -d --build
```
The tunnel does **not** need to be restarted; it automatically reconnects once `localhost:3000` is back online.
