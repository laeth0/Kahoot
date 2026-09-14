# Azure VM Operational & Maintenance Guide

This guide covers operational maintenance, configuration updates, and environment management for the Kahoot platform running on the **Azure Linux Virtual Machine** (`http://20.19.48.78/`) using **Docker Compose** and **Nginx Reverse Proxy**.

---

## 1. System Architecture & Live Deployment Context

The production application and its companion OpenTelemetry observability platform are containerized and managed via `docker-compose.prod.yml`:

```mermaid
flowchart TD
    subgraph Public["Public Internet (Hosts, Players, Operators)"]
        Users["Players & Quiz Hosts"]
        Ops["Authenticated Operators"]
    end

    subgraph AzureVM["Azure Linux VM (20.19.48.78: Standard D2s v3, 2 vCPU, 8 GB RAM)"]
        subgraph NSG["Network Security Group (NSG)"]
            P22["Port 22 (SSH Admin)"]
            P80["Port 80 (HTTP)"]
            P443["Port 443 (HTTPS - Optional)"]
        end

        subgraph IngressProxy["Ingress Proxy"]
            Nginx["Nginx Reverse Proxy\n(kahoot-nginx)\nListens on :80 / :443"]
        end

        subgraph AppNet["Docker Network: kahoot_internal (Bridge)"]
            Frontend["React 19 SPA\n(kahoot-frontend)\nInternal Port 80"]
            Backend["ASP.NET Core Web API + SignalR\n(kahoot-backend)\nInternal Port 8080"]
            Database[("PostgreSQL 18\n(kahoot-db)\nInternal Port 5432")]
        end

        subgraph ObsNet["Docker Network: observability_internal (internal: true)"]
            Collector["OTel Collector Contrib\n(kahoot-otel-collector)"]
            Prometheus[("Prometheus 3.14.0\n(kahoot-prometheus)")]
            Loki[("Grafana Loki 3.7.7\n(kahoot-loki)")]
            Jaeger[("Jaeger 2.20.0\n(kahoot-jaeger)")]
            Grafana["Grafana 13.2.1\n(kahoot-grafana)\nInternal Port 3000"]
            Exporters["Node, cAdvisor, Postgres & Blackbox Exporters"]
        end

        subgraph PersistentVolumes["Docker Named Volumes"]
            V_DB[("postgres_data")]
            V_Uploads[("uploads_data")]
            V_Prom[("prometheus_data")]
            V_Loki[("loki_data")]
            V_Jaeger[("jaeger_data")]
            V_Grafana[("grafana_data")]
            V_Logs[("nginx_logs")]
        end
    end

    Users -->|HTTP :80| P80 --> Nginx
    Ops -->|HTTP :80 /grafana/| P80 --> Nginx

    Nginx -->|/ & /assets/| Frontend
    Nginx -->|/api/ & /health| Backend
    Nginx <-->|/hubs/game (WebSockets)| Backend
    Nginx -->|/grafana/| Grafana

    Backend -->|OTLP gRPC :4317| Collector
    Collector --> Prometheus
    Collector --> Loki
    Collector --> Jaeger

    Grafana --> Prometheus
    Grafana --> Loki
    Grafana --> Jaeger
```

### Security & Network Port Rules:
- **Only Nginx** is exposed to host ports `80` and `443`.
- All other services (`backend`, `frontend`, `db`, `grafana`, `prometheus`, `loki`, `jaeger`, `otel-collector`, and exporters) reside strictly on internal Docker networks (`kahoot_internal` and `observability_internal`). They have **zero published host ports** and cannot be probed or scanned from the internet.
- **Operator Access:** Grafana is accessed securely via subpath proxy at `http://20.19.48.78/grafana/`.
- **Note on Azure Monitor Agent:** Azure Monitor Agent is not configured or required for this host; host and container telemetry are completely covered by Prometheus Node Exporter and cAdvisor. Azure Monitor Agent remains an optional future cloud integration.

---

## 2. Environment Configuration: Development vs. Production Separation

The repository maintains strict separation between local development and production environments.

### File Separation Overview

| Environment | Docker Compose File | Active Runtime Env File | Committed Git Template |
| :--- | :--- | :--- | :--- |
| **Local Development** | `docker-compose.yml` | `.env` | `.env.example` |
| **Azure VM Production** | `docker-compose.prod.yml` | `.env` (on VM) | `.env.production.example` |

> [!IMPORTANT]
> Git ignore rules in `.gitignore` are configured to ignore `.env`. Active runtime environment variables and secrets are kept in `.env` (untracked), while `.env.example` and `.env.production.example` serve as version-controlled templates.

---

## 3. What Values to Commit to Git vs. What Values to Keep Secret

When maintaining environment variables, follow this classification:

### A. Values That MUST Be Committed (in `.env.production.example`)
These are non-sensitive configuration keys, URLs, ports, and architectural defaults that describe how the production container stack runs:

| Variable | Recommended Committed Value | Purpose |
| :--- | :--- | :--- |
| `POSTGRES_DB` | `kahoot` | Standard PostgreSQL database name |
| `POSTGRES_USER` | `kahoot_admin` | Database username |
| `HTTP_PORT` | `80` | Public HTTP port mapped to Nginx |
| `HTTPS_PORT` | `443` | Public HTTPS port mapped to Nginx |
| `CLIENT_BASE_URL` | `http://20.19.48.78` | Canonical base URL used by backend `IJoinUrlGenerator` to produce `/join?pin={pin}` |
| `CORS_ALLOWED_ORIGINS` | `http://20.19.48.78,https://yourdomain.com` | Allowed browser origins for SignalR and API calls |
| `VITE_API_URL` | `/api` | Relative API path proxied through Nginx |
| `VITE_SIGNALR_URL` | `/hubs/game` | Relative SignalR hub path proxied through Nginx |
| `JWT_ISSUER` | `kahoot-api` | JWT token issuer |
| `JWT_AUDIENCE` | `kahoot-clients` | JWT token audience |
| `JWT_ACCESS_TOKEN_MINUTES` | `15` | Access token lifespan |
| `JWT_REFRESH_TOKEN_DAYS` | `14` | Refresh token lifespan |
| `HOST_SEED_USERNAME` | `IEEEXtreme Section` | Initial bootstrap admin username |

### B. Secrets That MUST NEVER Be Committed to Git
These values exist **only** inside the uncommitted `.env` file on the Azure VM server:

| Secret Variable | What It Is | How to Generate on the Azure VM |
| :--- | :--- | :--- |
| `POSTGRES_PASSWORD` | Master password for PostgreSQL database | `openssl rand -hex 24` |
| `JWT_SIGNING_KEY` | Symmetric HMAC-SHA256 secret (>= 32 chars) | `openssl rand -hex 32` |
| `HOST_SEED_PASSWORD` | Initial password for the host account | Strong unique passphrase |

---

## 4. Editing & Updating Environment Variables on the Azure VM

To update any environment configuration on your running Azure VM:

### Step 1: Connect to the VM
```bash
ssh -i kahoot-vm-key.pem azureuser@20.19.48.78
```

### Step 2: Navigate to Project Directory & Edit `.env`
```bash
cd /home/azureuser/kahoot
nano .env
```

### Step 3: Apply the Changes (Restart vs. Rebuild Rules)

Depending on which variables you changed, use the appropriate update command:

#### Rule A: Backend & Database Variables
- **Variables**: `CLIENT_BASE_URL`, `CORS_ALLOWED_ORIGINS`, `JWT_*`, `POSTGRES_*`, `HOST_SEED_*`
- **Action**: Backend reads these from runtime environment variables. A quick container restart is sufficient:
```bash
docker compose -f docker-compose.prod.yml up -d
# Or restart specifically:
docker compose -f docker-compose.prod.yml restart backend
```

#### Rule B: Frontend Variables
- **Variables**: `VITE_API_URL`, `VITE_SIGNALR_URL`
- **Action**: Vite inlines `import.meta.env` at build time into static JavaScript. If you modify any `VITE_*` variable, you **must rebuild** the frontend container:
```bash
docker compose -f docker-compose.prod.yml up -d --build frontend
```

#### Rule C: Port or Volume Changes
- **Variables**: `HTTP_PORT`, `HTTPS_PORT`
- **Action**: Docker Compose needs to recreate the Nginx container:
```bash
docker compose -f docker-compose.prod.yml up -d nginx
```

---

## 5. Editing Domain Name, Public URL, and CORS

When transitioning from the VM IP (`http://20.19.48.78`) to a custom domain (e.g. `https://kahoot.yourdomain.com`):

### 1. Update DNS
In your DNS registrar (Cloudflare, Namecheap, GoDaddy, Hostinger):
- Add an **A Record**: `@` (or subdomain `kahoot`) -> `20.19.48.78`.

### 2. Update Environment Variables on VM
In `/home/azureuser/kahoot/.env`:
```env
# Change from IP to your secure domain:
CLIENT_BASE_URL=https://kahoot.yourdomain.com
CORS_ALLOWED_ORIGINS=https://kahoot.yourdomain.com,http://20.19.48.78
```

### 3. Apply the Configuration
```bash
docker compose -f docker-compose.prod.yml up -d backend
```

> [!TIP]
> **Clipboard API Note**:
> When using `http://20.19.48.78/` (plain HTTP), modern browsers classify the site as an *insecure context* and disable `navigator.clipboard`. The application automatically uses a built-in `document.execCommand('copy')` fallback.
> Once you switch to a custom domain with HTTPS (`https://kahoot.yourdomain.com`), the browser recognizes it as a *secure context* and automatically re-activates the modern Async Clipboard API.

---

## 6. Managing Nginx & SSL Certificates (Let's Encrypt / Certbot)

### Initial SSL Certificate Setup with Certbot
Once your domain points to `20.19.48.78`:

1. Request the certificate using Certbot against the shared webroot volume:
```bash
docker run -it --rm \
  -v kahoot_certbot_conf:/etc/letsencrypt \
  -v kahoot_certbot_www:/var/www/certbot \
  certbot/certbot certonly --webroot \
  --webroot-path=/var/www/certbot \
  -d kahoot.yourdomain.com \
  --email admin@yourdomain.com \
  --agree-tos --no-eff-email
```

2. Edit `nginx/default.conf` to enable HTTPS and HTTP-to-HTTPS redirect:
```nginx
server {
    listen 80;
    server_name kahoot.yourdomain.com;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://$host$request_uri;
    }
}

server {
    listen 443 ssl;
    server_name kahoot.yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/kahoot.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/kahoot.yourdomain.com/privkey.pem;

    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    client_max_body_size 10M;

    location /health {
        proxy_pass http://backend:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /api/ {
        proxy_pass http://backend:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /uploads/ {
        proxy_pass http://backend:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /hubs/ {
        proxy_pass http://backend:8080;
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

    location / {
        proxy_pass http://frontend:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

3. Test configuration syntax and reload Nginx without downtime:
```bash
# Test syntax
docker exec kahoot-nginx nginx -t

# Reload configuration seamlessly
docker exec kahoot-nginx nginx -s reload
```

---

## 7. Deploying Code Updates to the Live Azure VM

When you push new bug fixes or features to the `main` branch:

```bash
# 1. SSH into the Azure VM
ssh -i kahoot-vm-key.pem azureuser@20.19.48.78

# 2. Navigate to project root
cd /home/azureuser/kahoot

# 3. Pull latest code
git pull origin main

# 4. Run deterministic static validation before deployment
bash observability/scripts/validate-config.sh

# 5. Rebuild and restart containers
docker compose -f docker-compose.prod.yml up -d --build --force-recreate

> [!TIP]
> Always use `--force-recreate` when rebuilding containers:
> ```bash
> docker compose -f docker-compose.prod.yml up -d --build --force-recreate
> ```
> This forces Nginx and proxy dependencies to re-resolve upstream container IP addresses immediately, preventing `502 Bad Gateway` errors from cached Docker bridge IPs.

# 6. Clean up stale/dangling images to free disk space
docker image prune -f

# 7. Verify containers are healthy
docker compose -f docker-compose.prod.yml ps

# 8. Run post-deployment observability verification
GRAFANA_ADMIN_USER=admin GRAFANA_ADMIN_PASSWORD="$GRAFANA_ADMIN_PASSWORD" GRAFANA_URL=http://20.19.48.78/grafana bash observability/scripts/validate-observability.sh
```

---

## 8. Database Operations & Maintenance

### 1. Migrations Workflow
- The backend features `DatabaseMigrationHostedService`.
- Every time `kahoot-backend` restarts or is redeployed, EF Core automatically applies any pending database migrations before opening HTTP traffic.
- To verify migration status, check the backend logs:
  ```bash
  docker compose -f docker-compose.prod.yml logs --tail=50 backend
  ```

### 2. Backing Up the Database
```bash
mkdir -p /home/azureuser/backups
docker exec kahoot-db pg_dump -U kahoot_admin -d kahoot | gzip > /home/azureuser/backups/kahoot_$(date +%Y%m%d_%H%M%S).sql.gz
```

### 3. Restoring from a Backup
```bash
gunzip -c /home/azureuser/backups/kahoot_YYYYMMDD_HHMMSS.sql.gz | docker exec -i kahoot-db psql -U kahoot_admin -d kahoot
```

### 4. Resetting Database to Clean State
```bash
docker compose -f docker-compose.prod.yml stop backend
docker exec -it kahoot-db psql -U postgres -c "DROP DATABASE IF EXISTS kahoot;"
docker exec -it kahoot-db psql -U postgres -c "CREATE DATABASE kahoot OWNER kahoot_admin;"
docker compose -f docker-compose.prod.yml start backend
```

---

## 9. Media Storage & Future Cloud Scaling

- **Current Architecture**: Images uploaded for quizzes are saved to `/app/uploads` and stored in the persistent Docker named volume `uploads_data`.
- **Zero Loss Guarantee**: The named volume survives container rebuilds, code updates, and VM reboots.
- **Future Cloud Evolution**:
  - The codebase implements a clean `IFileStorage` abstraction (`SaveAsync`).
  - If your workload scales past single-VM disk capacity, an `AzureBlobFileStorage` implementation can be plugged into `Kahoot.Infrastructure` without modifying any quiz or upload controllers.

---

## 10. Operational Troubleshooting & Health Diagnostics

| Symptom | Probable Cause | Diagnostic Command & Fix |
| :--- | :--- | :--- |
| **502 Bad Gateway from Nginx** | Backend starting up or Nginx cached stale container IP after a rebuild. | Run `docker compose -f docker-compose.prod.yml restart nginx` or rebuild with `--force-recreate`. Check backend logs: `docker compose -f docker-compose.prod.yml logs --tail=100 backend`. Check DB health: `docker compose -f docker-compose.prod.yml ps`. |
| **SignalR WebSockets disconnect or fallback to polling** | Missing WebSocket upgrade headers in Nginx. | Ensure `proxy_set_header Upgrade $http_upgrade;` and `proxy_set_header Connection "upgrade";` exist in the `/hubs/` location block in `nginx/default.conf`. |
| **CORS error in browser console** | Frontend origin does not match `CORS_ALLOWED_ORIGINS`. | Check origin in browser network tab. Update `CORS_ALLOWED_ORIGINS` in `.env` to include the exact scheme and domain (e.g. `http://20.19.48.78` or `https://kahoot.yourdomain.com`). Restart backend: `docker compose -f docker-compose.prod.yml restart backend`. |
| **Copy Join Link produces wrong URL** | `CLIENT_BASE_URL` in `.env` is incorrect or missing. | Update `CLIENT_BASE_URL` in `.env` to match the actual public host (e.g. `http://20.19.48.78` or `https://yourdomain.com`). Restart backend. |
| **404 on page reload (`/host/quizzes`, `/play/...`)** | Nginx trying to resolve client routes as static files. | Verify the `/` location block in `nginx/default.conf` proxies to `http://frontend:80;`. The frontend container's internal Nginx handles SPA fallback to `index.html`. |
| **Disk space running low** | Old Docker build cache or images accumulating. | Run `docker system prune -a --volumes=false -f` to clean unused images without touching persistent database/upload volumes. |

### Fast Health Check Verification Command:
```bash
curl -i http://20.19.48.78/health
# Expected Output: HTTP/1.1 200 OK -> Healthy
```
