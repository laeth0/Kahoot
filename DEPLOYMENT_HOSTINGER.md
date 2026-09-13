# Hostinger Production Deployment Guide

This guide provides step-by-step instructions to deploy the Kahoot application on **Hostinger** in a secure, production-ready environment.

---

## 1. Hosting Architecture Overview

Hostinger offers two primary types of hosting plans:

| Plan Type | Supported Capabilities | Recommendation |
| :--- | :--- | :--- |
| **Hostinger VPS** (Ubuntu 22.04 / 24.04) | Full root access, Docker, Docker Compose, .NET 10 Kestrel runtime, PostgreSQL 17, WebSockets, systemd | **Strongly Recommended (Full Stack)** |
| **Hostinger Web Hosting** (Cloud / Shared) | Static HTML/JS/CSS, PHP, MySQL, Apache/LiteSpeed (`.htaccess`) | **Frontend Only** (Backend + DB must run on VPS) |

> [!IMPORTANT]
> The .NET 10 Web API and PostgreSQL 17 database require persistent background server processes and WebSocket support. **Hostinger VPS** is the recommended option to host the complete stack using Docker Compose.

---

## 2. Option A: Hostinger VPS Deployment with Docker Compose (Recommended)

### Step 1: Prepare the Hostinger VPS
1. In your Hostinger control panel, create an **Ubuntu 22.04** or **Ubuntu 24.04** VPS.
2. Connect to your VPS via SSH:
   ```bash
   ssh root@<your_vps_ip>
   ```
3. Update package index and install Docker & Docker Compose:
   ```bash
   sudo apt-get update && sudo apt-get upgrade -y
   sudo apt-get install -y ca-certificates curl gnupg git

   # Install Docker Engine & Docker Compose plugin
   sudo install -m 0755 -d /etc/apt/keyrings
   curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
   sudo chmod a+r /etc/apt/keyrings/docker.gpg

   echo \
     "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
     $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
     sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

   sudo apt-get update
   sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
   ```

### Step 2: Clone Repository & Configure Secrets
1. Clone the project repository on the VPS:
   ```bash
   git clone <your_repository_url> /opt/kahoot
   cd /opt/kahoot
   ```
2. Copy the production environment template:
   ```bash
   cp .env.production.example .env
   ```
3. Edit `.env` with production secrets:
   ```bash
   nano .env
   ```
   Configure the following required values:
   - `POSTGRES_PASSWORD`: generate with `openssl rand -hex 24`
   - `JWT_SIGNING_KEY`: generate with `openssl rand -base64 48` (minimum 32 characters)
   - `HOST_SEED_USERNAME`: initial host login username (e.g. `Admin`)
   - `HOST_SEED_PASSWORD`: strong password for host account
   - `CORS_ALLOWED_ORIGINS`: your production domain(s), e.g. `https://yourdomain.com`
   - `VITE_API_URL`: `/api`
   - `VITE_SIGNALR_URL`: `/hubs/game`

### Step 3: Launch Containers
Run the production compose file:
```bash
docker compose -f docker-compose.prod.yml up -d --build
```
Verify that all containers are healthy:
```bash
docker compose -f docker-compose.prod.yml ps
```
Check backend startup and database migration logs:
```bash
docker compose -f docker-compose.prod.yml logs -f backend
```
*Note: Database migrations run automatically on container startup via `DatabaseMigrationHostedService`.*

---

## 3. Configuring Domain & SSL on Hostinger VPS

To secure your site with HTTPS (SSL), install Nginx on the host VPS as an SSL termination reverse proxy.

### Step 1: Point Your Domain
In your Hostinger DNS Zone Editor:
- Add an **A Record**: `@` pointing to `<your_vps_ip>`
- Add an **A Record**: `www` pointing to `<your_vps_ip>`

### Step 2: Configure Host Nginx & Certbot (Let's Encrypt)
1. Install Nginx and Certbot on the VPS:
   ```bash
   sudo apt-get install -y nginx certbot python3-certbot-nginx
   ```
2. Edit `/etc/nginx/sites-available/kahoot`:
   ```nginx
   server {
       server_name yourdomain.com www.yourdomain.com;

       client_max_body_size 10M;

       location / {
           proxy_pass http://127.0.0.1:80;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection "upgrade";
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
           proxy_read_timeout 3600s;
           proxy_send_timeout 3600s;
       }
   }
   ```
   *(If host Nginx occupies port 80, set `HTTP_PORT=8080` in `.env` and `proxy_pass http://127.0.0.1:8080;` in host Nginx).*
3. Enable the site and obtain free SSL certificate:
   ```bash
   sudo ln -s /etc/nginx/sites-available/kahoot /etc/nginx/sites-enabled/
   sudo nginx -t
   sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com
   ```
Certbot will automatically configure HTTPS redirects and auto-renewal.

---

## 4. Option B: Hostinger Cloud Web Hosting (Frontend Only)

If you have a Hostinger Web Hosting plan for your frontend domain and a separate VPS for the backend:

1. Build the production React frontend with your backend API domain:
   ```bash
   cd frontend
   VITE_API_URL=https://api.yourdomain.com/api VITE_SIGNALR_URL=https://api.yourdomain.com/hubs/game npm run build
   ```
2. In Hostinger hPanel:
   - Navigate to **Files** -> **File Manager**.
   - Open `public_html/`.
   - Upload all contents from `frontend/dist/` into `public_html/`.
   - Ensure `public_html/.htaccess` is present (copied automatically from `frontend/public/.htaccess`).
3. Ensure the backend `.env` has:
   ```env
   CORS_ALLOWED_ORIGINS=https://yourfrontenddomain.com
   ```

---

## 5. Production Checklist

Verify each of the following before going live:

| Item | Requirement | Verification Method |
| :--- | :--- | :--- |
| **1. Environment Variables** | `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, `CORS_ALLOWED_ORIGINS` configured | Check `.env` file |
| **2. Database Migrations** | Automatically applied on startup | Inspect `docker logs kahoot-backend-prod` |
| **3. File Uploads Persistence** | Volume `uploads_data:/app/uploads` attached | Restart container, check uploaded image persists |
| **4. Max Upload Size** | Supports up to 5 MB images | Upload a 3 MB image in quiz editor |
| **5. SPA Client Routing** | Direct navigation to `/host/quizzes` or `/play/:id` does not 404 | Refresh non-root browser URL |
| **6. SignalR WebSockets** | Real-time connection establishes without falling back | Inspect DevTools Network -> WS filter |
| **7. HTTPS / SSL** | Valid TLS certificate, no mixed-content warnings | Visit `https://yourdomain.com` in browser |
| **8. Security Isolation** | Database port 5432 is internal only (not exposed) | `nc -zv <vps_ip> 5432` should fail/timeout |
| **9. Health Endpoint** | Returns 200 OK | `curl -f https://yourdomain.com/health` |

---

## 6. Common Troubleshooting & Fixes

### 1. `413 Request Entity Too Large` on image upload
- **Cause:** Nginx default upload limit is 1MB.
- **Fix:** Ensure `client_max_body_size 10M;` is present in `frontend/nginx.conf` and host Nginx.

### 2. `WebSocket connection to 'wss://.../hubs/game' failed`
- **Cause:** Reverse proxy missing WebSocket headers or closing idle connections.
- **Fix:** Verify `proxy_set_header Upgrade $http_upgrade;`, `proxy_set_header Connection "upgrade";`, and `proxy_read_timeout 3600s;` in Nginx.

### 3. `CORS header 'Access-Control-Allow-Origin' missing`
- **Cause:** Frontend domain does not match `CORS_ALLOWED_ORIGINS`.
- **Fix:** Update `CORS_ALLOWED_ORIGINS=https://yourdomain.com` in `.env` and restart backend.

### 4. `The 'Jwt' configuration section is missing` or `SigningKey must be >= 32 characters`
- **Cause:** `JWT_SIGNING_KEY` is missing or shorter than 32 characters.
- **Fix:** Generate a key with `openssl rand -base64 48` and place it in `.env`.

### 5. Uploaded images return 404 after redeployment
- **Cause:** Volume `uploads_data` was not mounted or named volume was removed.
- **Fix:** Always deploy using `docker compose -f docker-compose.prod.yml` with named volume `uploads_data:/app/uploads`.
