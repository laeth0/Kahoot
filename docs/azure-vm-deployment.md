# Azure Ubuntu Virtual Machine Deployment Guide

This guide provides step-by-step instructions for deploying the Kahoot-like platform on an **Azure Ubuntu Virtual Machine** using **Docker Compose** and **Nginx Reverse Proxy**.

This architecture is optimized to support **200+ concurrent players** with real-time SignalR WebSockets and zero publicly exposed internal ports.

---

## 1. System Architecture

```mermaid
flowchart TD
    Internet["Public Internet (Hosts & 200+ Players)"]

    subgraph AzureVM["Azure Virtual Machine (Ubuntu 22.04 / 24.04 LTS)"]
        subgraph NSG["Azure Network Security Group (NSG)"]
            P22["Port 22 (SSH - Admin Only)"]
            P80["Port 80 (HTTP)"]
            P443["Port 443 (HTTPS)"]
        end

        subgraph DockerBridge["Docker Isolated Network (kahoot_internal)"]
            Nginx["Nginx Reverse Proxy\n(kahoot-nginx-prod)\nListens on 80 / 443"]
            Frontend["React 19 SPA\n(kahoot-frontend-prod)\nInternal Port 80"]
            Backend["ASP.NET Core Web API\n(kahoot-backend-prod)\nInternal Port 8080"]
            Database[("PostgreSQL 18\n(kahoot-db-prod)\nInternal Port 5432")]
        end

        subgraph Storage["Persistent Docker Volumes"]
            V_DB[("postgres_data\n(/var/lib/postgresql)")]
            V_Uploads[("uploads_data\n(/app/uploads)")]
            V_Certs[("certbot_conf\n(/etc/letsencrypt)")]
        end
    end

    Internet -->|SSH| P22
    Internet -->|HTTP| P80 --> Nginx
    Internet -->|HTTPS| P443 --> Nginx

    Nginx -->|/ & /assets/| Frontend
    Nginx -->|/api/ & /health| Backend
    Nginx <-->|/hubs/game (WebSockets)| Backend
    Nginx -->|/uploads/| Backend
    Backend -->|Host=db:5432| Database

    Database --- V_DB
    Backend --- V_Uploads
    Nginx --- V_Uploads
    Nginx --- V_Certs
```

### Port Security Matrix

| Port | Service | Access Level | Purpose |
| :--- | :--- | :--- | :--- |
| **22** | SSH | Public / Restricted to Admin IP | Remote server administration |
| **80** | HTTP | Public | Initial HTTP traffic & SSL challenge |
| **443** | HTTPS | Public | Encrypted application traffic |
| **5432** | PostgreSQL | **Blocked / Internal Only** | Unexposed to host or internet |
| **5000** | Backend API | **Blocked / Internal Only** | Unexposed to host or internet |
| **3000** | Frontend | **Blocked / Internal Only** | Unexposed to host or internet |

---

## 2. Step-by-Step Azure Portal UI Deployment

### Step 1: Create the Azure Ubuntu Virtual Machine

1. Log in to the [Azure Portal](https://portal.azure.com).
2. In the top search bar, type **Virtual machines** and select it.
3. Click **Create** > **Azure virtual machine**.
4. In the **Basics** tab, configure:
   - **Subscription**: Select your active subscription.
   - **Resource group**: Click **Create new** and enter `kahoot-vm-rg`.
   - **Virtual machine name**: Enter `kahoot-production-vm`.
   - **Region**: Choose the closest region (e.g. `(Europe) West Europe` or `(US) East US`).
   - **Availability options**: `No infrastructure redundancy required`.
   - **Security type**: `Standard`.
   - **Image**: Select **Ubuntu Server 24.04 LTS - x64 Gen2** (or `Ubuntu Server 22.04 LTS`).
   - **Size**: Select **Standard_B2s** (2 vCPUs, 4 GiB memory) or **Standard_D2s_v5**.
     > *Note: 4 GiB RAM comfortably supports 200 concurrent WebSockets and PostgreSQL connection pooling.*
   - **Authentication type**: Select **SSH public key**.
   - **Username**: Enter `azureuser`.
   - **SSH public key source**: Select **Generate new key pair** (or use an existing public key).
   - **Key pair name**: `kahoot-vm-key`.
   - **Public inbound ports**: Select **Allow selected ports** and check **SSH (22)**, **HTTP (80)**, and **HTTPS (443)**.
5. In the **Disks** tab:
   - **OS disk type**: Select **Standard SSD** (or Premium SSD) with `30 GiB` or `64 GiB`.
6. Click **Review + create**, verify details, then click **Create**.
7. Download the private key (`kahoot-vm-key.pem`) to your local machine when prompted.

---

### Step 2: Configure Azure Network Security Group (NSG)

1. In the Azure Portal, navigate to your virtual machine: **kahoot-production-vm**.
2. In the left navigation menu under **Settings**, click **Networking** (or **Network settings**).
3. Verify your **Inbound port rules**:
   - Priority 300: `SSH` | Port `22` | Protocol `TCP` | Action `Allow`
   - Priority 310: `HTTP` | Port `80` | Protocol `TCP` | Action `Allow`
   - Priority 320: `HTTPS` | Port `443` | Protocol `TCP` | Action `Allow`
4. Confirm that ports `5432`, `5000`, and `3000` are **NOT** listed in the inbound rules. Any port not explicitly allowed is blocked by Azure's default `DenyAllInBound` rule.
5. Under **Networking**, click on the **NIC Public IP** link and set the assignment to **Static** so the IP address remains fixed across restarts. Note your VM's **Public IP address**.

---

### Step 3: Connect to the VM via SSH

On your local machine (macOS / Linux terminal or Windows PowerShell):

1. Set correct permissions for the downloaded SSH private key:
   ```bash
   # On macOS / Linux:
   chmod 400 kahoot-vm-key.pem

   # On Windows PowerShell:
   icacls.exe .\kahoot-vm-key.pem /inheritance:r
   icacls.exe .\kahoot-vm-key.pem /grant:r "$($env:USERNAME):R"
   ```

2. Connect to the Azure VM:
   ```bash
   ssh -i kahoot-vm-key.pem azureuser@<YOUR_VM_PUBLIC_IP>
   ```

---

### Step 4: Install Docker & Docker Compose on Ubuntu

Run the following commands inside your SSH session on the VM:

```bash
# Update package lists and install prerequisites
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg lsb-release git

# Add Docker's official GPG key
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Set up the Docker repository
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker Engine and Docker Compose plugin
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Allow azureuser to run docker without sudo
sudo usermod -aG docker $USER

# Apply group changes immediately
newgrp docker

# Verify installations
docker --version
docker compose version
```

---

### Step 5: Upload the Project to the VM

Inside the SSH session, clone the repository:

```bash
# Clone the repository
git clone https://github.com/<YOUR_GITHUB_USERNAME>/kahoot.git /home/azureuser/kahoot

# Navigate to project directory
cd /home/azureuser/kahoot
```

*(Alternatively, you can transfer files from your local machine using `scp` or SFTP).*

---

### Step 6: Configure Environment Variables

1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```

2. Generate secure production secrets:
   ```bash
   # Generate secure random values
   openssl rand -hex 24   # Use for POSTGRES_PASSWORD
   openssl rand -hex 32   # Use for JWT_SIGNING_KEY
   ```

3. Edit `.env` using nano:
   ```bash
   nano .env
   ```

4. Configure production settings:
   ```env
   POSTGRES_DB=kahoot
   POSTGRES_USER=kahoot_admin
   POSTGRES_PASSWORD=<Paste_Generated_Postgres_Password>

   JWT_SIGNING_KEY=<Paste_Generated_JWT_Key_At_Least_32_Chars>
   JWT_ISSUER=kahoot-api
   JWT_AUDIENCE=kahoot-clients
   JWT_ACCESS_TOKEN_MINUTES=15
   JWT_REFRESH_TOKEN_DAYS=14

   HOST_SEED_USERNAME=IEEEXtreme Section
   HOST_SEED_PASSWORD=<Your_Strong_Host_Password>

   CORS_ALLOWED_ORIGINS=https://yourdomain.com

   VITE_API_URL=/api
   VITE_SIGNALR_URL=/hubs/game

   HTTP_PORT=80
   HTTPS_PORT=443
   ```
   *Press `Ctrl + O`, `Enter` to save, then `Ctrl + X` to exit.*

---

### Step 7: Launch the Production Stack

Start the production containers using `docker-compose.prod.yml`:

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

Verify that all containers are healthy and running:

```bash
docker compose -f docker-compose.prod.yml ps
```

You should see:
- `kahoot-db-prod`: `Up (healthy)`
- `kahoot-backend-prod`: `Up`
- `kahoot-frontend-prod`: `Up`
- `kahoot-nginx-prod`: `Up` (Ports `0.0.0.0:80->80/tcp`, `0.0.0.0:443->443/tcp`)

Check backend logs to confirm database migrations and host account creation completed:

```bash
docker compose -f docker-compose.prod.yml logs -f backend
```
*(Press `Ctrl + C` to stop watching logs).*

---

### Step 8: Configure Your Domain Name

1. Open your DNS provider (e.g. Cloudflare, Namecheap, GoDaddy, Hostinger DNS).
2. Add an **A Record**:
   - **Host / Name**: `@`
   - **Value / Points to**: `<YOUR_AZURE_VM_PUBLIC_IP>`
   - **TTL**: Auto or 300 seconds
3. Add a second **A Record** (optional):
   - **Host / Name**: `www`
   - **Value / Points to**: `<YOUR_AZURE_VM_PUBLIC_IP>`

Wait a few minutes for DNS to propagate. Test resolution:
```bash
ping yourdomain.com
```

---

### Step 9: Configure HTTPS with Let's Encrypt (Certbot)

1. **Obtain SSL Certificate using Certbot**:
   Run Certbot using Docker against the `certbot_www` volume:
   ```bash
   docker run -it --rm \
     -v kahoot_certbot_conf:/etc/letsencrypt \
     -v kahoot_certbot_www:/var/www/certbot \
     certbot/certbot certonly --webroot \
     --webroot-path=/var/www/certbot \
     -d yourdomain.com -d www.yourdomain.com \
     --email admin@yourdomain.com \
     --agree-tos --no-eff-email
   ```

2. **Update `nginx/default.conf` for HTTPS**:
   Edit `nginx/default.conf` to enable SSL and redirect HTTP to HTTPS:
   ```bash
   nano nginx/default.conf
   ```

   Replace contents with:
   ```nginx
   server {
       listen 80;
       server_name yourdomain.com www.yourdomain.com;

       location /.well-known/acme-challenge/ {
           root /var/www/certbot;
       }

       location / {
           return 301 https://$host$request_uri;
       }
   }

   server {
       listen 443 ssl;
       server_name yourdomain.com www.yourdomain.com;

       ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
       ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;

       ssl_protocols TLSv1.2 TLSv1.3;
       ssl_ciphers HIGH:!aNULL:!MD5;
       ssl_prefer_server_ciphers on;
       ssl_session_cache shared:SSL:10m;
       ssl_session_timeout 1d;

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

3. **Reload Nginx**:
   ```bash
   docker compose -f docker-compose.prod.yml restart nginx
   ```

---

## 3. Database Operations Reference

### 1. How to Backup the Database
```bash
# Create backup directory
mkdir -p /home/azureuser/backups

# Export database dump to a compressed file with timestamp
docker exec kahoot-db-prod pg_dump -U kahoot_admin -d kahoot | gzip > /home/azureuser/backups/kahoot_$(date +%Y%m%d_%H%M%S).sql.gz

# Verify backup file exists
ls -lh /home/azureuser/backups
```

### 2. How to Restore the Database
```bash
# Decompress and restore SQL dump
gunzip -c /home/azureuser/backups/kahoot_2026xxxx.sql.gz | docker exec -i -e PGPASSWORD="<your_password>" kahoot-db-prod psql -U kahoot_admin -d kahoot
```

### 3. How to Reset the Database (Clean Slate)
```bash
# Stop backend to release active database connections
docker compose -f docker-compose.prod.yml stop backend

# Drop and recreate the database
docker exec -it kahoot-db-prod psql -U postgres -c "DROP DATABASE IF EXISTS kahoot;"
docker exec -it kahoot-db-prod psql -U postgres -c "CREATE DATABASE kahoot OWNER kahoot_admin;"

# Start backend (migrations will automatically run on startup)
docker compose -f docker-compose.prod.yml start backend
```

### 4. How to Apply Migrations
Migrations run automatically on container startup via `DatabaseMigrationHostedService`. If you wish to trigger them manually:
```bash
docker compose -f docker-compose.prod.yml restart backend
```

---

## 4. Performance & 200 Concurrent Users Tuning

The architecture is tuned for 200 concurrent active users:

1. **Nginx Worker Connections**:
   `worker_connections 1024` in `nginx/nginx.conf` easily handles 200 concurrent persistent WebSocket connections + API calls.
2. **Kestrel Concurrency**:
   ASP.NET Core Kestrel handles 1,000+ concurrent connections per core without blocking.
3. **PostgreSQL Connection Pool**:
   Npgsql connection pool configured with `Maximum Pool Size=100`. Database queries in `SubmitAnswer` execute in milliseconds, allowing 100 connections to service 200+ rapid submissions without queue exhaustion.
4. **Zero Polling & Efficient Scoring**:
   SignalR WebSockets push events directly to clients. Answer submissions update scores atomically (`ExecuteUpdateAsync`) without loading complete entities into memory.

---

## 5. Verification Checklist

After deployment, test the full application flow:

1. **Health Check**:
   ```bash
   curl -i http://<YOUR_VM_IP>/health
   # Expected: HTTP/1.1 200 OK -> Healthy
   ```
2. **Host Authentication**:
   - Open `https://yourdomain.com/login`
   - Log in with `HOST_SEED_USERNAME` and `HOST_SEED_PASSWORD`.
3. **Quiz & Image Flow**:
   - Create a quiz and upload a question image.
   - Verify image loads correctly at `https://yourdomain.com/uploads/...`.
4. **Live Gameplay**:
   - Launch the game as Host (observe 6-digit PIN).
   - Open `https://yourdomain.com/join` on a mobile device or separate browser window.
   - Enter PIN and player nickname.
   - Host clicks **Start Game** -> Question displays simultaneously on Host and Player screen.
   - Submit an answer -> Feedback is instant.
   - Host clicks **End Question** / **Leaderboard** -> Leaderboard updates in real-time.
   - Host clicks **End Game** -> Podium displays correctly.
