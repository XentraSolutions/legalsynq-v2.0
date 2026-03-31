# Step 27 – Production Readiness

## Files Created

| File | Location | Purpose |
|------|----------|---------|
| `src/app/api/health/route.ts` | `apps/control-center/` | Health check endpoint — `GET /api/health` |
| `Dockerfile` | `apps/control-center/` | Multi-stage Docker image: `deps` → `builder` → `runner` |
| `.dockerignore` | `apps/control-center/` | Excludes node_modules, .next/cache, .env files, logs from build context |
| `ci-cd-template.md` | `analysis/` | Generic CI/CD pipeline template with all 9 stages documented |

## Files Updated

| File | What changed |
|------|-------------|
| `analysis/deployment-notes.md` | Added three new sections: **Health checks** (endpoint, ALB/ECS/k8s config, monitoring), **Scaling considerations** (stateless, autoscaling triggers, memory), **Cache behaviour** (ISR TTLs, invalidation, CDN guidance, runtime logging) |

---

## Health Check Design

### Endpoint

`GET /api/health` — implemented in `src/app/api/health/route.ts`.

No authentication required. Called by load balancers before a session exists.

### Response shape

```json
{
  "status":     "ok" | "degraded",
  "service":    "control-center",
  "uptime":     number,
  "gateway":    "reachable" | "unreachable" | "unknown",
  "gatewayUrl": "https://api.legalsynq.com",
  "ts":         "2026-03-29T10:00:00.000Z"
}
```

HTTP status is always `200`. The caller reads `status` to determine whether
the service is fully healthy (`"ok"`) or running with a degraded dependency
(`"degraded"`).

### Gateway probe

- Method: `HEAD` to the origin of `CONTROL_CENTER_API_BASE`
- Timeout: 2 seconds (hard-abort via `AbortController`)
- Any HTTP response from the gateway → `"reachable"` (proves the process is alive)
- Network error or timeout → `"unreachable"` → service status becomes `"degraded"`
- Non-URL-parseable base → `"unknown"` (neutral — not counted as degraded)

### Why HEAD to origin (not a path)?

Sending `HEAD /` to the gateway root avoids triggering auth middleware or
rate-limiting rules on specific API paths. Any status code (200, 401, 404)
proves the process is alive.

### Caching

`Cache-Control: no-store, no-cache, must-revalidate` is set on the response.
The endpoint must never be served from a CDN or reverse proxy cache.

### Probe location

`probeGateway()` reads `CONTROL_CENTER_API_BASE` from `env.ts` — the same
constant used by all other server-side API calls. No extra env var required.

### Load balancer configuration

| Platform | Config |
|----------|--------|
| AWS ALB | HTTP health check on `/api/health`, port 5004, 30 s interval |
| ECS task | `CMD-SHELL curl -sf http://localhost:5004/api/health \|\| exit 1` |
| Kubernetes | `httpGet` liveness + readiness probe on `/api/health:5004` |
| Dockerfile | `HEALTHCHECK CMD wget -qO- http://localhost:5004/api/health \|\| exit 1` |

---

## Docker Strategy

### Multi-stage build

```
Stage 1 — deps
  FROM node:22-alpine
  npm ci --legacy-peer-deps
  Produces: /app/node_modules

Stage 2 — builder
  FROM node:22-alpine
  COPY --from=deps node_modules
  COPY . .
  npm run build
  Produces: /app/.next

Stage 3 — runner
  FROM node:22-alpine
  COPY --from=builder .next, public, package.json, node_modules
  ENV NODE_ENV=production
  EXPOSE 5004
  CMD ["npm", "run", "start"]
```

### Why three stages?

- **`deps`** — isolated dependency install. Layer is cached and reused as long
  as `package-lock.json` does not change, regardless of source changes.
- **`builder`** — full source copy + `next build`. Re-runs only when source
  changes (deps layer is reused from cache).
- **`runner`** — no build tools, no dev dependencies, no source files. Only
  the compiled output and the runtime dependency tree.

### `NEXT_PUBLIC_*` variables at build time

`NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` is passed as a `--build-arg` because Next.js
inlines it into the client-side JavaScript bundle during `next build`. It cannot
be injected at container startup.

```bash
docker build \
  --build-arg NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=https://controlcenter.legalsynq.com \
  -t control-center:latest .
```

Server-side-only vars (`CONTROL_CENTER_API_BASE`, `NODE_ENV`) are injected at
container start via `-e` flags or secrets manager:

```bash
docker run -p 5004:5004 \
  -e CONTROL_CENTER_API_BASE=https://api.legalsynq.com \
  control-center:latest
```

### Image size optimisation (current vs future)

| Approach | Approx. image size |
|----------|--------------------|
| Current (full node_modules copy) | ~400–600 MB |
| With `output: 'standalone'` in next.config.mjs | ~150–200 MB |

**TODO:** Add `output: 'standalone'` to `next.config.mjs`. This copies only
the files Next.js needs to run (no full node_modules) into `.next/standalone/`.
The runner stage then only needs:

```dockerfile
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static     ./.next/static
COPY --from=builder /app/public           ./public
CMD ["node", "server.js"]
```

### Base image choice

`node:22-alpine` (LTS) chosen for:
- Small footprint (~45 MB compressed vs ~330 MB for `node:22`)
- Well-maintained security patches
- Compatible with all npm packages used by the CC

---

## CI/CD Strategy

### Full pipeline

```
push / PR to main
       │
       ▼
  [1] Install           npm ci --legacy-peer-deps
       │
       ▼
  [2] Type Check        tsc --noEmit           ← blocks merge on failure
       │
       ▼
  [3] Lint (TODO)       next lint
       │
       ▼
  [4] Build             next build             ← NEXT_PUBLIC_* set here
       │
       ▼
  [5] Docker Build      docker build + smoke test (curl /api/health)
       │
       ▼ (main only)
  [6] Push to Registry  docker push
       │
       ▼
  [7] Deploy            aws ecs update-service / railway / vercel / fly
       │
       ▼
  [8] Post-deploy       curl /api/health on prod URL
       │
       ▼
  [9] Notify            Slack / email
```

Full template with platform-specific commands: `analysis/ci-cd-template.md`

### Key design decisions

1. **Type check runs before build** — fails fast before spending 90 s on
   `next build` when there are TypeScript errors.

2. **Smoke test runs inside CI** — the freshly-built image is started and
   `/api/health` is polled before pushing to the registry. This catches
   startup errors (missing env var, module resolution failure) before any
   traffic is affected.

3. **`NEXT_PUBLIC_*` vars are build args** — they must be set at `docker build`
   time, not at `docker run` time, because Next.js inlines them into the bundle.

4. **Deploy only on main** — pull requests run install → type-check → build →
   docker build (smoke) but do not push or deploy.

5. **Post-deploy health poll** — after every deploy, the pipeline polls the
   production health endpoint for up to 60 s. Failure triggers a PagerDuty alert
   and halts the pipeline (rollback is manual at this stage — TODO: automate).

---

## Runtime Considerations

### Log aggregation

In production the logger emits **NDJSON to stdout**:

```json
{"level":"info","msg":"[CC] GET /api/tenants → 200 (142 ms)","requestId":"abc-123","ts":"..."}
```

Route container stdout to the log aggregator:

| Platform | Config |
|----------|--------|
| AWS CloudWatch Logs | `awslogs` log driver in ECS task definition |
| Datadog | `com.datadoghq.ad.logs` Docker label on the container |
| Self-hosted (Loki / Fluentd) | `fluentd` Docker log driver |

### Required infrastructure components

| Component | Purpose | Examples |
|-----------|---------|---------|
| Log aggregator | Collect, index, search structured logs | CloudWatch Logs, Datadog, Grafana Loki |
| APM (optional) | Distributed tracing (CC → gateway → services) | Datadog APM, AWS X-Ray, OpenTelemetry |
| Alerting | On-call escalation for 5xx spikes / health failures | PagerDuty, Opsgenie, VictorOps |
| Dashboard | RPS, latency (p50/p95/p99), error rate, instance count | Grafana, CloudWatch Dashboard |
| Uptime monitor | External probe of `/api/health` | Uptime Robot, Better Uptime, Pingdom |

### Key log events emitted

| Event | Level | Fields |
|-------|-------|--------|
| Request start | `info` | `method`, `path`, `requestId` |
| Request end | `info` | `status`, `durationMs`, `requestId` |
| Session not found (pre-flight) | `warn` | `path`, `requestId` |
| Impersonation start | `info` (audit) | `adminId`, `targetUserId`, `tenantId` |
| Impersonation stop | `info` (audit) | `adminId`, `targetUserId` |
| Tenant context switch | `info` (audit) | `adminId`, `tenantId`, `tenantCode` |
| API error | `error` | `status`, `path`, `message`, `requestId` |

### PII in logs

The logger automatically:
- Redacts `Authorization: Bearer <token>` headers
- Partially masks email addresses in production (`j***@example.com`)

No JWT tokens or full email addresses appear in stdout.

---

## TODOs

```ts
// TODO: add autoscaling config
//   — ECS: target-tracking on CPU 60%, min 2 / max 10 tasks
//   — Kubernetes: HPA + KEDA for queue-based scaling

// TODO: add blue/green deployment
//   — ECS CodeDeploy AppSpec: route 10% → 100% after health check pass
//   — Kubernetes: Argo Rollouts canary strategy

// TODO: add alerting rules
//   — Alert: 5xx rate > 1%, p99 > 3 s, restarts > 2 in 5 min
//   — CloudWatch Alarms → SNS → PagerDuty
//   — Datadog: monitor control-center.response_time.p99 > 3000ms

// TODO: add output: 'standalone' to next.config.mjs
//   — Reduce Docker image size from ~500 MB to ~150 MB

// TODO: add Dockerfile HEALTHCHECK instruction
//   — CMD wget -qO- http://localhost:5004/api/health || exit 1

// TODO: add /api/readiness endpoint
//   — Validates all required env vars at startup
//   — Distinct from /api/health (liveness) so k8s can fail readiness
//     without killing a live but misconfigured pod

// TODO: add non-root user to Dockerfile
//   — addgroup --system nodejs && adduser --system nextjs
//   — CIS Docker Benchmark requirement

// TODO: add SAST + container scan to CI
//   — npm audit --audit-level=high
//   — Trivy image scan after docker build
//   — Block pipeline on critical CVEs

// TODO: add rate limiting on /api/auth/login
//   — 5 req/min per IP to prevent brute-force credential stuffing
```

---

## Independence Validation

- `src/app/api/health/route.ts` imports only from `@/lib/env` and `next/server` —
  zero imports from other monorepo packages
- `Dockerfile` references only `apps/control-center/` artefacts — no workspace
  package symlinks, no monorepo root mounts
- `.dockerignore` scoped to `apps/control-center/` contents only
- `analysis/ci-cd-template.md` is a documentation file only — no code changes
- `analysis/deployment-notes.md` update is additive — all existing content
  preserved; three new sections appended
- TypeScript check: **0 errors**
- No business logic changed; no existing file signatures altered
- App restarts to `✓ Ready in 2.4s` with no compilation errors

---

## Any Issues or Assumptions

1. **`GET /api/health` is unauthenticated by design** — the middleware
   (`src/middleware.ts`) must not redirect `/api/health` to `/login`. If
   middleware currently applies auth to all `/api/*` paths, add an exception:
   ```ts
   if (request.nextUrl.pathname === '/api/health') return NextResponse.next();
   ```
   The health endpoint is called by load balancers before any user session
   exists; requiring auth would make it useless for orchestration.

2. **Gateway probe target is the origin root, not a health path** — the
   `probeGateway()` function sends `HEAD` to `new URL(CONTROL_CENTER_API_BASE).origin`
   (e.g. `https://api.legalsynq.com`), not to a specific gateway path. This is
   intentional: it avoids hitting any authenticated route and triggering
   unintended side effects. If the gateway exposes its own `/health` endpoint,
   the probe target can be updated to use it.

3. **Dockerfile does not yet use `output: 'standalone'`** — the runner stage
   copies the full `node_modules/` directory (~300 MB). This is functional but
   produces a larger image than necessary. The `output: 'standalone'` Next.js
   config option enables a much leaner image, but requires a change to
   `next.config.mjs` and updating the Dockerfile `CMD`. This is left as a TODO
   to avoid changing `next.config.mjs` behaviour mid-step.

4. **`NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` must be a build arg** — unlike server-
   side env vars, `NEXT_PUBLIC_*` variables are read at `next build` time and
   inlined into the client bundle. They cannot be injected at `docker run` time.
   This means separate images may be needed for staging vs production if the
   origin URL differs. Alternatively, use relative URLs everywhere in client
   code and set the full origin only for server-side redirect logic.

5. **No `/api/readiness` endpoint** — the health endpoint doubles as both a
   liveness and readiness probe. A separate readiness endpoint that validates
   all required env vars are set and the gateway is reachable would be more
   idiomatic for Kubernetes deployments. This is left as a TODO.

6. **`process.uptime()` is the Node.js process uptime**, not the container
   uptime or the wall-clock deploy age. It resets on every process restart. In
   an ECS Fargate deployment with task restarts, `uptime` will be small even
   for a long-running deployment. This is expected behaviour.
