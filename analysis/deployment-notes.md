# Control Center — Deployment Notes

## Overview

The Control Center (`apps/control-center`) is a standalone Next.js 14 App Router
application that can be deployed independently of the other LegalSynq services.
It communicates with the platform only through the API gateway — it has no direct
database access and no imports from other monorepo packages.

---

## Required environment variables

| Variable | Required | Description |
|----------|----------|-------------|
| `CONTROL_CENTER_API_BASE` | **yes (prod)** | Base URL of the API gateway. All server-side requests (session checks, tenant/user/support data) go through this URL. Example: `https://api.legalsynq.com` |
| `NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` | no | Canonical public URL of the Control Center. Used for absolute redirects and links. Falls back to `REPLIT_DEV_DOMAIN` → `http://localhost:5004`. Example: `https://controlcenter.legalsynq.com` |
| `GATEWAY_URL` | no | Legacy alias for `CONTROL_CENTER_API_BASE`. Accepted as a fallback but the explicit var is preferred. |
| `NEXT_PUBLIC_BASE_PATH` | no | URL sub-path prefix when mounted behind a reverse proxy (e.g. `/admin`). Leave blank in most deployments. |

### Missing variable behaviour

In **production** (`NODE_ENV=production`), `getEnv()` throws at startup if a
required variable is absent. The app will fail to start with a clear error
message rather than failing silently at request time.

In **development**, `getEnv()` logs a `WARN` to the console and returns an empty
string, allowing the hot-reload loop to continue.

---

## Local development

```bash
# 1. Copy the env template
cp apps/control-center/.env.example apps/control-center/.env.local

# 2. Fill in CONTROL_CENTER_API_BASE (gateway port)
echo "CONTROL_CENTER_API_BASE=http://localhost:5010" >> apps/control-center/.env.local

# 3. Start the Control Center on port 5004
cd apps/control-center
pnpm dev       # or: npm run dev
```

The full monorepo dev script (`scripts/run-dev.sh`) starts all services together
including the Control Center on `:5004` and the gateway on `:5010`.

---

## Standalone production build

```bash
cd apps/control-center

# Install dependencies
npm install --legacy-peer-deps

# Set required env vars (or use a .env file)
export CONTROL_CENTER_API_BASE=https://api.legalsynq.com
export NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=https://controlcenter.legalsynq.com
export NODE_ENV=production

# Build
npm run build   # outputs to .next/

# Start
npm run start   # listens on port 5004
```

---

## Subdomain configuration

### Recommended: `controlcenter.legalsynq.com`

The Control Center is designed to run at a dedicated subdomain. To configure:

1. **DNS** — Add an A record or CNAME:
   ```
   controlcenter.legalsynq.com  →  <server-IP or load-balancer>
   ```

2. **Reverse proxy (nginx / Caddy / ALB)** — Route traffic to the Next.js
   process on port 5004:
   ```nginx
   server {
     server_name controlcenter.legalsynq.com;
     location / {
       proxy_pass http://127.0.0.1:5004;
       proxy_set_header Host $host;
       proxy_set_header X-Forwarded-Proto $scheme;
       proxy_set_header X-Forwarded-Host $host;
     }
   }
   ```

3. **Environment variable** — Set `NEXT_PUBLIC_CONTROL_CENTER_ORIGIN`:
   ```
   NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=https://controlcenter.legalsynq.com
   ```

4. **Session cookie scope** — The `platform_session` cookie is currently scoped
   to the Control Center domain (path: `/`). For cross-subdomain sessions
   (so the main web app and CC share a session) set the cookie domain to
   `.legalsynq.com`. This requires updating the BFF login/logout route handlers.

### Tenant-specific subdomains

The login route handler (`POST /api/auth/login`) can extract the tenant code
from the subdomain:

```
hartwell.controlcenter.legalsynq.com  →  tenantCode = "HARTWELL"
legalsynq.controlcenter.legalsynq.com →  tenantCode = "LEGALSYNQ"
```

This is handled by `extractTenantCodeFromHost()` in `src/app/api/auth/login/route.ts`
when `x-forwarded-host` or `host` has at least 3 dot-separated segments.

---

## Security headers

All responses from the Control Center include the following HTTP security
headers (configured in `next.config.mjs`):

| Header | Value | Environment |
|--------|-------|-------------|
| `X-Frame-Options` | `SAMEORIGIN` | All |
| `X-Content-Type-Options` | `nosniff` | All |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | All |
| `X-DNS-Prefetch-Control` | `off` | All |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=()` | All |
| `Strict-Transport-Security` | `max-age=63072000; includeSubDomains; preload` | **Production only** |

---

## Cookie security summary

| Cookie | httpOnly | secure | sameSite |
|--------|----------|--------|----------|
| `platform_session` | `true` | `true` (prod) | `strict` (prod) / `lax` (dev) |
| `cc_impersonation` | `true` | `true` (prod) | `strict` (prod) / `lax` (dev) |
| `cc_tenant_context` | `false` | `true` (prod) | `strict` (prod) / `lax` (dev) |

---

## Port reference

| Service | Port | Notes |
|---------|------|-------|
| Next.js web | 5000 | Main tenant-facing web app |
| Control Center | **5004** | Admin-only control plane |
| API Gateway | 5010 | All Control Center server-side calls go here |
| Identity service | 5001 | Proxied by gateway |
| Fund service | 5002 | Proxied by gateway |
| CareConnect service | 5003 | Proxied by gateway |

---

## TODOs

```
TODO: add Dockerfile
  — Multi-stage build: node:22-alpine for build, distroless for runtime.
  — Copy .next/, public/, package.json, node_modules into the final image.
  — EXPOSE 5004 + CMD ["node", "server.js"].

TODO: add CI/CD pipeline
  — On merge to main: run tsc --noEmit, next build, docker build, push to ECR.
  — Deploy to ECS Fargate / Railway / Vercel on successful image push.
  — Required secrets in CI: CONTROL_CENTER_API_BASE, NEXT_PUBLIC_CONTROL_CENTER_ORIGIN.

TODO: add health check endpoint
  — GET /api/health → { status: "ok", service: "control-center", version: "...", uptime: N }
  — Used by load balancer and container orchestration health checks.
  — Should verify CONTROL_CENTER_API_BASE is reachable (e.g. HEAD /identity/api/auth/me).

TODO: add Content-Security-Policy header
  — Requires audit of all inline scripts and external resources.

TODO: add rate limiting on /api/auth/login
  — Prevent brute-force credential stuffing.
```
