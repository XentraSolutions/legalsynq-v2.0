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

## Health checks

### Endpoint

```
GET /api/health
```

Response shape:

```json
{
  "status":     "ok",
  "service":    "control-center",
  "uptime":     42.7,
  "gateway":    "reachable",
  "gatewayUrl": "https://api.legalsynq.com",
  "ts":         "2026-03-29T10:00:00.000Z"
}
```

`status` is `"ok"` when the gateway HEAD probe succeeds; `"degraded"` when the
gateway is unreachable. The HTTP status is always `200` — the caller inspects
`status` to decide whether to alert.

The gateway probe is a HEAD request to the root of `CONTROL_CENTER_API_BASE`
with a 2-second timeout. Any HTTP response (including 401, 404) is treated as
"reachable" — it proves the gateway process is alive.

### Load balancer / container orchestration

Configure the health check to poll `GET /api/health` and expect HTTP `200`:

**AWS ALB target group:**

```
Protocol: HTTP
Path:     /api/health
Port:     5004
Healthy threshold:   2 consecutive successes
Unhealthy threshold: 3 consecutive failures
Interval:            30 s
Timeout:             5 s
```

**ECS task definition (container health check):**

```json
{
  "command": ["CMD-SHELL", "curl -sf http://localhost:5004/api/health || exit 1"],
  "interval": 30,
  "timeout":  5,
  "retries":  3,
  "startPeriod": 60
}
```

**Kubernetes liveness + readiness probe:**

```yaml
livenessProbe:
  httpGet:
    path: /api/health
    port: 5004
  initialDelaySeconds: 30
  periodSeconds: 30
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /api/health
    port: 5004
  initialDelaySeconds: 10
  periodSeconds: 10
  failureThreshold: 3
```

**Dockerfile `HEALTHCHECK`:**

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD wget -qO- http://localhost:5004/api/health || exit 1
```

### Monitoring

- **CloudWatch / Datadog**: scrape `/api/health` every 30 s; alert if `status` is
  `"degraded"` for 3 consecutive checks.
- **Uptime Robot / Better Uptime**: point an HTTP monitor at
  `https://controlcenter.legalsynq.com/api/health`.
- **Response time alerting**: alert on p99 latency > 3 s or any 5xx from the
  health endpoint itself.

---

## Scaling considerations

### Stateless process

The Control Center process is fully stateless — all session state lives in the
`platform_session` HttpOnly cookie (a JWT issued by the Identity service). Any
number of identical instances can serve the same user without sticky sessions.

### Horizontal scaling

Run ≥ 2 instances behind a load balancer in production to eliminate single
points of failure. Instances can be scaled independently of all other services.

**Recommended minimums:**

| Environment | Min instances | Max instances |
|-------------|--------------|--------------|
| Staging | 1 | 2 |
| Production | 2 | 10 |

### Autoscaling triggers

Scale out when:
- CPU > 60% sustained for 3 minutes
- Memory > 75% sustained for 3 minutes
- Request count > 500 req/min per instance

Scale in when:
- CPU < 20% for 15 minutes (with cooldown)

### Memory baseline

Typical Next.js 14 App Router process at rest: ~150–200 MB RSS.
Set the container memory limit to **512 MB** per instance to allow for request
burst headroom.

### Cold start

Next.js 14 in `next start` mode has no cold start beyond normal Node.js process
startup time (~2–3 s). There are no Lambda/edge cold-start concerns unless
deploying to Vercel Edge Functions (not currently planned).

### Sticky sessions

Not required. The app is stateless. Configure the load balancer with round-robin
or least-connections routing.

---

## Cache behaviour

### Next.js fetch cache (server-side)

The Control Center uses React's extended `fetch` with `next.revalidate` (ISR)
for data that changes infrequently:

| Data | TTL | Cache tag |
|------|-----|-----------|
| Tenant list | 60 s | `tenants` |
| Tenant detail | 30 s | `tenant:{id}` |
| User list | 30 s | `users` |
| Support tickets | 60 s | `support-tickets` |
| Fund accounts | 60 s | `fund-accounts` |

`revalidateTag()` is called from Server Actions that mutate the corresponding
data, so stale reads are bounded to the TTL only when no mutation occurs.

### Invalidation

Server Actions that write data call `revalidateTag(CACHE_TAGS.tenants)` (or the
relevant tag) immediately after a successful mutation. The next request after
invalidation fetches fresh data from the gateway.

### `/api/health` caching

The health endpoint explicitly sets `Cache-Control: no-store`. It must never be
served from a CDN or proxy cache — it must always hit the live process.

### Static assets

Next.js automatically serves `/_next/static/` assets with a long-lived
`Cache-Control: public, max-age=31536000, immutable` header. These are content-
hashed so there is no stale-asset risk on deploy.

### CDN / reverse proxy

If placing a CDN (CloudFront, Fastly) in front of the Control Center:

1. **Cache the static `/_next/static/` prefix** with `max-age=31536000`.
2. **Never cache** `/_next/data/`, `/api/`, or any page route — they are
   dynamic (server-rendered with per-request auth).
3. **Forward cookies** (`platform_session`, `cc_tenant_context`,
   `cc_impersonation`) to the origin — they must not be stripped.
4. **Forward headers** `X-Request-Id`, `X-Forwarded-Host`, `X-Forwarded-Proto`.

---

## Runtime logging

### Log format

In production (`IS_PROD=true`) the structured logger (`src/lib/logger.ts`)
emits **newline-delimited JSON** (NDJSON) to stdout:

```json
{"level":"info","msg":"[CC] GET /api/tenants → 200 (142 ms)","requestId":"abc-123","ts":"2026-03-29T10:00:00.000Z"}
```

In development it emits human-readable ANSI coloured text to stdout.

### Log aggregation

Route stdout from the container to your log aggregator:

**CloudWatch Logs (ECS Fargate):**

```json
{
  "logDriver": "awslogs",
  "options": {
    "awslogs-group":  "/ecs/control-center",
    "awslogs-region": "eu-west-1",
    "awslogs-stream-prefix": "ecs"
  }
}
```

**Datadog (Docker label):**

```dockerfile
LABEL com.datadoghq.ad.logs='[{"source":"nodejs","service":"control-center"}]'
```

**Self-hosted (Fluentd / Loki):**

```yaml
# docker-compose logging driver
logging:
  driver: fluentd
  options:
    fluentd-address: localhost:24224
    tag: control-center
```

### Required infrastructure

| Tool | Purpose |
|------|---------|
| Log aggregator (CloudWatch / Datadog / Loki) | Collect and index stdout NDJSON |
| APM agent (optional but recommended) | Distributed tracing across gateway → CC → browser |
| Alerting (PagerDuty / Opsgenie) | On-call escalation for 5xx spikes or health check failures |
| Dashboard (Grafana / CloudWatch) | RPS, latency (p50/p95/p99), error rate, instance count |

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
TODO: add autoscaling config
  — ECS: target-tracking policy on CPU 60% with min 2 / max 10 tasks.
  — Kubernetes: HPA on CPU + RPS with KEDA for queue-based scaling.

TODO: add blue/green deployment
  — ECS: use CodeDeploy AppSpec with blue/green task set routing.
  — Only shift 100% traffic after post-deploy health check passes.

TODO: add alerting rules
  — Alert on: 5xx rate > 1%, p99 latency > 3 s, pod restarts > 2 in 5 min.
  — CloudWatch Alarms → SNS → PagerDuty.
  — Datadog: monitor "control-center.response_time.p99 > 3000ms".

TODO: add Content-Security-Policy header
  — Requires audit of all inline scripts and external resources.

TODO: add rate limiting on /api/auth/login
  — Prevent brute-force credential stuffing (e.g. 5 req/min per IP).

TODO: add output: 'standalone' to next.config.mjs
  — Reduces Docker image size significantly.
  — Required if switching to standalone CMD ["node", ".next/standalone/server.js"].

TODO: add /api/readiness endpoint
  — Validates all required env vars are set on startup.
  — Distinct from /api/health (liveness) so Kubernetes can fail readiness
    without killing a live but misconfigured pod.
```
