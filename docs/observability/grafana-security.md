# Grafana Security & Authentication Architecture

This guide explains the security model, credential management, network protections, and operational risks associated with accessing the Grafana observability interface over HTTP on the Azure Linux Virtual Machine (`http://20.19.48.78/grafana/`).

---

## 1. Authentication Model

- **Built-in Authentication:** Grafana uses its local SQLite user database for username and password authentication.
- **Anonymous Access Disabled:** `GF_AUTH_ANONYMOUS_ENABLED: "false"` is strictly enforced across both development and production Compose configurations. Unauthenticated visitors cannot access dashboards, explore queries, or view alert states.
- **Self-Registration Disabled:** `GF_USERS_ALLOW_SIGN_UP: "false"` prevents unauthorized external users from creating accounts.
- **External Identity Providers Not Configured:** In this deployment phase, OAuth, SSO, Microsoft Entra ID, Google, GitHub, and SAML identity providers are intentionally not configured. All access relies on locally provisioned accounts.

---

## 2. Password Bootstrapping & Rotation Procedure

### Initial Bootstrapping
Initial administrator credentials are read from the untracked `.env.production` file on the Azure VM at container startup:
```ini
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=<strong-random-password>
```
Grafana applies these credentials on its first start.

### Admin Password Rotation
To rotate the administrator password without restarting Grafana or recreating volumes:
```bash
# Execute password reset directly inside the running container
docker compose exec kahoot-grafana grafana-cli admin reset-admin-password "<new-strong-password>"
```
After resetting, update `.env.production` on the host to keep the environment synchronized for any future cold boots.

---

## 3. Network Isolation & Proof of Port Privacy

Grafana and all telemetry backends are hosted exclusively on the private Docker network `observability_internal` with `internal: true`.

- **Only Nginx is exposed on the host:**
  - Nginx publishes port `80` (and `443` if SSL is active).
  - Port `3000` (Grafana), `9090` (Prometheus), `3100` (Loki), `16686` (Jaeger), `4317`/`4318` (OTel Collector), and exporter ports `9100`, `9115`, `9187` are **NEVER published to the host**.
- **Verification Command:**
  Confirm that private ports cannot be reached from the public internet or local host network:
  ```bash
  for port in 3000 3100 4317 4318 9090 9100 9115 9187 16686; do
    curl --connect-timeout 1 http://20.19.48.78:${port} || echo "Port ${port} is securely blocked"
  done
  ```

---

## 4. HTTP Transport Security & Risk Disclosure

> [!WARNING]
> **HTTP Cleartext Credential Exposure:**
> Because the application target on `http://20.19.48.78/` is currently accessed over unencrypted plain HTTP:
> 1. **No Transport Confidentiality or Integrity:** Passwords, session cookies, and dashboard metrics travel over the public internet in cleartext. Any intermediary on the network path (Wi-Fi access points, ISPs, network taps) can potentially intercept or alter this traffic.
> 2. **Cookie Flags:** `GF_SECURITY_COOKIE_SECURE=false` is required so browsers store the session cookie over HTTP. However, `GF_SECURITY_COOKIE_SAMESITE=strict` is enforced to defend against Cross-Site Request Forgery (CSRF).

### Recommended Security Practices for Operators
1. **Strong, Unique Password:** Never reuse personal or corporate passwords for `GRAFANA_ADMIN_PASSWORD`. Generate at least 24 random hex/alphanumeric characters.
2. **Restrict Azure NSG Inbound IPs:** Configure the Azure Network Security Group (NSG) on port `80` to allow inbound traffic only from trusted operator IP addresses or through a VPN whenever feasible.
3. **Avoid Public Wi-Fi:** Never log into Grafana from untrusted or public Wi-Fi networks without an encrypted VPN tunnel.
4. **Dedicated Accounts:** Do not share the admin account for routine read-only dashboard viewing.

---

## 5. Future HTTPS Upgrade Path

When a custom domain name (e.g. `kahoot.yourdomain.com`) is assigned to the Azure VM, HTTPS should be enabled immediately:

1. Obtain a free Let's Encrypt certificate using the pre-configured Certbot volume mounts in `docker-compose.prod.yml`.
2. Enable port `443` in the Azure Network Security Group.
3. In `docker-compose.prod.yml`, update Grafana environment settings:
   ```yaml
   GF_SECURITY_COOKIE_SECURE: "true"
   GF_SERVER_ROOT_URL: "https://yourdomain.com/grafana/"
   ```
4. This ensures full end-to-end transport encryption, eliminating the credential and cookie interception risks inherent in plain HTTP.
