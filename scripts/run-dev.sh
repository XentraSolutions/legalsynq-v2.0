#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NODE="$(command -v node)"
[ -n "$NODE" ] || { echo "ERROR: 'node' not found in PATH. Install Node.js first." >&2; exit 1; }

echo "====== LegalSynq dev startup ======"

# Start Next.js on an internal port; the proxy on :5000 gates requests
# until the cold-compile race condition is resolved (HTTP 200 on /login).
NEXT_INTERNAL_PORT=3050
echo "[web] Starting Next.js on :$NEXT_INTERNAL_PORT (internal)"
# Use the pnpm store binary for Next.js 16.
PNPM_NEXT16="$ROOT/node_modules/.pnpm/next@16.2.6_@playwright+test@1.59.1_react-dom@18.3.1_react@18.3.1__react@18.3.1/node_modules/next"
WEB_NEXT_BIN="$PNPM_NEXT16/dist/bin/next"
if [ ! -f "$WEB_NEXT_BIN" ]; then
  WEB_NEXT_BIN="$(find "$ROOT/node_modules/.pnpm" -path "*/next@16*/node_modules/next/dist/bin/next" 2>/dev/null | head -1)"
fi
if [ -z "$WEB_NEXT_BIN" ] || [ ! -f "$WEB_NEXT_BIN" ]; then
  echo "[web] WARNING: Could not find Next.js 16 binary in pnpm store, falling back to root"
  WEB_NEXT_BIN="$ROOT/node_modules/next/dist/bin/next"
fi
echo "[web] Using next binary: $WEB_NEXT_BIN"
# Pin apps/web/node_modules/next → pnpm store 16 so webpack resolves consistently.
WEB_NM="$ROOT/apps/web/node_modules"
if [ -d "$PNPM_NEXT16" ]; then
  mkdir -p "$WEB_NM"
  rm -rf "$WEB_NM/next"
  ln -s "$PNPM_NEXT16" "$WEB_NM/next"
  mkdir -p "$WEB_NM/.bin"
  rm -f "$WEB_NM/.bin/next"
  ln -s "../next/dist/bin/next" "$WEB_NM/.bin/next"
  echo "[web] Pinned node_modules/next → 16.2.6"
fi
# Clear stale .next build artefacts so Next.js 16 dev mode starts fresh
# and does not fail looking for required-server-files.json from a prior build.
rm -rf "$ROOT/apps/web/.next"
(cd "$ROOT/apps/web" && GATEWAY_URL=http://localhost:5010 \
  CC_COMMON_PORTAL_HOSTNAME="${CC_COMMON_PORTAL_HOSTNAME:-careconnect-demo.legalsynq.com}" \
  exec "$NODE" "$WEB_NEXT_BIN" dev -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting dev proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 "$NODE" "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

# Start Control Center — port 5004
# Pin the CC's node_modules/next to Next.js 16 so webpack uses the correct version.
echo "[control-center] Starting Next.js on :5004"
CC_NM="$ROOT/apps/control-center/node_modules"
if [ -d "$PNPM_NEXT16" ]; then
  mkdir -p "$CC_NM/.bin"
  rm -rf "$CC_NM/next"
  ln -s "$PNPM_NEXT16" "$CC_NM/next"
  rm -f "$CC_NM/.bin/next"
  ln -s "../next/dist/bin/next" "$CC_NM/.bin/next"
  echo "[control-center] Pinned node_modules/next → 16.2.6"
fi
CC_NEXT_BIN="$PNPM_NEXT16/dist/bin/next"
if [ ! -f "$CC_NEXT_BIN" ]; then
  CC_NEXT_BIN="$(find "$ROOT/node_modules/.pnpm" -path "*/next@16*/node_modules/next/dist/bin/next" 2>/dev/null | head -1)"
fi
if [ -z "$CC_NEXT_BIN" ] || [ ! -f "$CC_NEXT_BIN" ]; then
  echo "[control-center] WARNING: Could not find Next.js 16 binary, falling back to root binary"
  CC_NEXT_BIN="$ROOT/node_modules/next/dist/bin/next"
fi
echo "[control-center] Using next binary: $CC_NEXT_BIN"
# Clear stale .next artefacts for control-center as well.
rm -rf "$ROOT/apps/control-center/.next"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 MONITORING_SOURCE=service exec "$NODE" "$CC_NEXT_BIN" dev -p 5004) &
PID_CC=$!

# Restore, build, and start .NET services all in background
(
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity quiet
  # The full solution build can OOM on constrained hosts. Add || true so the
  # subshell continues and services launch from their cached (pre-built) binaries.
  dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] LegalSynq.sln build error (possibly OOM) — continuing with cached binaries"
  dotnet build "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" --configuration Debug --verbosity quiet \
    || echo "[build] Documents.Api build error — continuing with cached binary"
  # Flow service has its own solution (separate boundary, separate DB).
  # Build only the API project — not the full Flow.sln — so test-project
  # NuGet packages (never restored by LegalSynq.sln) don't block the build.
  dotnet restore "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Flow.Api build error — continuing with cached binary"
  # Reports service has its own project boundary (not in LegalSynq.sln).
  dotnet restore "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Reports.Api build error — continuing with cached binary"
  # Audit service has its own project boundary (not in LegalSynq.sln).
  dotnet build   "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" --configuration Debug --verbosity quiet \
    || echo "[build] Audit build error — continuing with cached binary"
  # Support service has its own solution boundary (not in LegalSynq.sln).
  dotnet restore "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Support.Api build error — continuing with cached binary"
  # Commerce service (port 5030) — separate project boundary, not in LegalSynq.sln.
  # Pre-build here so no inline build is needed once services start competing for memory.
  dotnet restore "$ROOT/apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[commerce] Build error — continuing with cached binary"
  # Tenant Billing service (port 5031) — separate project boundary, not in LegalSynq.sln.
  dotnet restore "$ROOT/apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[billing] Build error — continuing with cached binary"
  dotnet build "$ROOT/apps/services/xenia/Xenia.Api/Xenia.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[xenia] Build error — continuing with cached binary"
  # Identity.Api can OOM inside the full solution build on constrained hosts.
  # Build it separately here with conservative GC settings so the binary always
  # reflects the latest source (including the "role" claim fix in JwtTokenService).
  # Falls back to the cached binary if this also fails.
  echo "[identity] Building Identity.Api with conservative memory settings..."
  DOTNET_GCConserveMemory=9 \
    dotnet build "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" \
    --no-restore --configuration Debug --verbosity quiet -maxcpucount:1 \
    || echo "[identity] Build error — will run from cached binary"
  # Tenant.Api needs a separate low-memory build for the "role" RoleClaimType fix.
  # Pre-building here means no inline build is needed during the memory-constrained
  # service startup phase.
  echo "[tenant] Building Tenant.Api with conservative memory settings..."
  DOTNET_GCConserveMemory=9 \
    dotnet build "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj" \
    --no-restore --configuration Debug --verbosity quiet -maxcpucount:1 \
    || echo "[tenant] Build error — will run from cached binary"

  # ── Service startup ──────────────────────────────────────────────────────────
  # Gateway starts FIRST: it is stateless (no DB migration) and needs memory
  # before the 15+ other services are initialized.  Once all DB services are
  # running they collectively consume enough RAM to trigger OOM in any process
  # that tries to start a thread pool gate thread.
  # Explicit ASPNETCORE_URLS=5010 overrides launchSettings so the correct port
  # is always used regardless of which profile dotnet run selects.
  # DOTNET_GCConserveMemory=9 reduces the GC's memory footprint.
  # ── Start-order rationale ──────────────────────────────────────────────────
  # 1. Gateway (no DB) — must grab memory before 16 other processes load.
  # 2. Monitoring — starts 2nd so only 1 process is running when it initialises.
  #    Monitoring's OOM was killing Task and Flow when it started 15th; moving it
  #    here gives it maximum RAM headroom. It will emit "no data" for most
  #    entities at first, which is fine — the monitoring cycle auto-recovers.
  # 3. Commerce + Billing + Xenia (stateless/in-memory) — lightweight, start while RAM is free.
  # 4. Tenant (has DB migration) — starts early to avoid OOM when last.
  # 5. Reports + Task — also OOM'd when last; moved to positions 5–6.
  # 6. Remaining DB services — staggered 3 s apart to avoid MySQL connection
  #    storm (AWS RDS limit ~150 connections shared across all services).
  # 7. Support — last; it has its own separate MySQL instance.
  # All services use DOTNET_GCConserveMemory=9 to shrink per-process GC heap.
  # ──────────────────────────────────────────────────────────────────────────

  echo "[gateway] Starting Gateway on :5010..."
  ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://0.0.0.0:5010 \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
  sleep 5

  # Monitoring starts 2nd — only 1 other process (Gateway) is running.
  # The retry wrapper restarts once on crash and prefixes all output with [monitoring].
  (
    for attempt in 1 2; do
      echo "[monitoring] Starting Monitoring service (attempt $attempt)..."
      ASPNETCORE_ENVIRONMENT=Development \
        DOTNET_GCConserveMemory=9 \
        dotnet run --no-build --project "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" \
        2>&1 | sed 's/^/[monitoring] /' || true
      if [ "$attempt" -lt 2 ]; then
        echo "[monitoring] Service exited on attempt $attempt; restarting in 15s..."
        sleep 15
      fi
    done
    echo "[monitoring] Monitoring service exited after 2 attempts."
  ) &
  sleep 5

  # Commerce, Billing, and Xenia are stateless/in-memory; start early.
  ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://0.0.0.0:5030 \
    DOTNET_GCConserveMemory=9 \
    COMMERCE_LEGALSYNQ_SIGNING_KEY="${Jwt__SigningKey:-}" \
    dotnet run --no-build --project "$ROOT/apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://0.0.0.0:5031 \
    DOTNET_GCConserveMemory=9 \
    BILLING_LEGALSYNQ_SIGNING_KEY="${Jwt__SigningKey:-}" \
    dotnet run --no-build --project "$ROOT/apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://0.0.0.0:5032 \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/xenia/Xenia.Api/Xenia.Api.csproj" &
  sleep 3

  # Tenant starts after the stateless services so DB-backed startup still happens early.
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj" &
  sleep 5

  # Reports, Task, and Flow are all moved early for the same OOM reason —
  # they OOM'd when placed late in the 17-service startup wave.
  # Flow.Api binds to the port set in its launchSettings (5075 default) but
  # the monitoring entity "Workflow" checks 5012; explicit ASPNETCORE_URLS pins
  # it to 5012 to match.
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/task/Task.Api/Task.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    ASPNETCORE_URLS=http://0.0.0.0:5012 \
    dotnet run --no-build --project "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" &
  sleep 5

  # Remaining DB-backed services — staggered to avoid MySQL connection storm.
  # All use DOTNET_GCConserveMemory=9 to reduce per-process GC heap footprint,
  # leaving more memory for the processes that start later in the sequence.
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    NotificationsService__BaseUrl=http://127.0.0.1:5008 \
    NotificationsService__PortalBaseUrl=http://localhost:3000 \
    NotificationsService__CareConnectPortalBaseUrl=http://careconnect-demo.localhost:3000 \
    dotnet run --no-build --project "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    AppBaseUrl="${PORTAL_BASE_URL:-http://localhost:3000}" \
    AppBaseDomain="${Route53__BaseDomain:-}" \
    TenantService__BaseUrl=http://127.0.0.1:5005 \
    TenantService__ProvisioningToken="${TenantService__ProvisioningToken:-}" \
    dotnet run --no-build --project "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    ASPNETCORE_URLS=http://0.0.0.0:5007 \
    dotnet run --no-build --project "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/comms/Comms.Api/Comms.Api.csproj" &
  sleep 3
  # Support service — port 5017, standalone MySQL, JWT via Authentication:Jwt:SymmetricKey.
  # Authentication__Jwt__SymmetricKey is sourced from Jwt__SigningKey (the same secret used
  # by the gateway) so tokens minted by the platform validate correctly in Support.
  # Falls back to the matching dev key when no secret is set in the environment.
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    Authentication__Jwt__SymmetricKey="${Jwt__SigningKey:-dev-only-signing-key-minimum-32-chars-long!}" \
    dotnet run --no-build --project "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" &
  wait
) &
PID_DOTNET=$!

# Start artifacts API server — port 5020
echo "[artifacts] Starting on :5020"
(
  cd "$ROOT/artifacts/api-server"
  ARTIFACTS_PORT=5020 NODE_ENV=development \
    node_modules/.bin/ts-node-dev --respawn --transpile-only src/server.ts
) &
PID_ARTIFACTS=$!

cleanup() {
    kill "$PID_WEB" "$PID_PROXY" "$PID_CC" "$PID_DOTNET" "$PID_ARTIFACTS" 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait "$PID_WEB" "$PID_PROXY" "$PID_CC" "$PID_DOTNET" "$PID_ARTIFACTS"
