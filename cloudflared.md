# Cloudflare Tunnel Setup

Steps to run and expose the application publicly using Cloudflare Tunnel.

---

## 1. Start the Application

Make sure the Docker containers are running:

```powershell
docker compose up -d
```

Verify it is running locally:
```powershell
curl http://localhost:3000/health
```

---

## 2. Start Cloudflare Tunnel

In the project root, run:

```powershell
.\cloudflared.exe tunnel --protocol http2 --url http://localhost:3000
```

---

## 3. Get Public URL

Copy the public URL generated in the terminal logs:

```text
https://<random-subdomain>.trycloudflare.com
```

---

## 4. Access the Application

- **Players / Join**: `https://<random-subdomain>.trycloudflare.com`
- **Host Dashboard**: `https://<random-subdomain>.trycloudflare.com/host`
  - **Username**: `IEEEXtreme Section`
  - **Password**: `IEEEXtreme@123456789`

---

## 5. (Optional) Run Load Tests

Update `BASE_URL` and `SIGNALR_URL` in `load-tests/.env` with the new URL:

```env
BASE_URL=https://<random-subdomain>.trycloudflare.com/api
SIGNALR_URL=https://<random-subdomain>.trycloudflare.com
```

Then run tests:

```powershell
node load-tests/run-all.js answer-burst -- -e PLAYERS=500 -e JOIN_RAMP=30s -e ANSWER_TIME_LIMIT=15 -e SETTLE_SECONDS=10
```

