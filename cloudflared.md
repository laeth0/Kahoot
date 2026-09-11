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

---

## Troubleshooting: `DNS_PROBE_FINISHED_NXDOMAIN` (Link Not Opening)

If the link does not open on Windows or mobile, the local router or ISP DNS is either caching negative results or filtering `*.trycloudflare.com`. Apply these steps to fix it immediately:

### Windows (Chrome)
1. Open `chrome://settings/security` in Chrome.
2. Enable **Use secure DNS**.
3. Select **With**, then choose **Cloudflare (1.1.1.1)** or **Google (Public DNS)**.
4. Reload the tunnel page.

### Mobile (Android)
1. Open **Settings** -> **Connections** (or **Network & Internet**).
2. Select **More connection settings** -> **Private DNS**.
3. Choose **Private DNS provider hostname** and enter:
   ```text
   one.one.one.one
   ```
   *(or `dns.google`)*
4. Save and reload the page in your mobile browser.

### Mobile (iPhone / iOS)
1. Open **Settings** -> **Wi-Fi**.
2. Tap the **(i)** icon next to your connected Wi-Fi network.
3. Scroll down and tap **Configure DNS** -> select **Manual**.
4. Tap **Add Server** and enter `1.1.1.1` and `8.8.8.8`.
5. Tap **Save** and reload the page in Safari/Chrome.


