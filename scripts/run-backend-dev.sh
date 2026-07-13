#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "====== LegalSynq backend dev startup ======"

require_free_port() {
  local port="$1"
  local label="$2"
  local pids
  pids="$(lsof -ti tcp:"$port" 2>/dev/null || true)"
  if [ -n "$pids" ]; then
    echo "ERROR: Port :$port is already in use before startup ($label)." >&2
    echo "PID(s): $pids" >&2
    echo "Run 'bash scripts/stop-dev.sh' to clear leftover LegalSynq dev processes," >&2
    echo "or stop the conflicting process manually and retry." >&2
    exit 1
  fi
}

require_free_port 5010 "gateway"
require_free_port 5020 "artifacts API"

# Restore, build, and start .NET services all in background
(
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity quiet
  dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] LegalSynq.sln build error (possibly OOM) — continuing with cached binaries"
  dotnet build "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" --configuration Debug --verbosity quiet \
    || echo "[build] Documents.Api build error — continuing with cached binary"
  dotnet restore "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Flow.Api build error — continuing with cached binary"
  dotnet restore "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Reports.Api build error — continuing with cached binary"
  dotnet build   "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" --configuration Debug --verbosity quiet \
    || echo "[build] Audit build error — continuing with cached binary"
  dotnet restore "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Support.Api build error — continuing with cached binary"
  dotnet restore "$ROOT/apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[commerce] Build error — continuing with cached binary"
  dotnet restore "$ROOT/apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[billing] Build error — continuing with cached binary"
  echo "[identity] Building Identity.Api with conservative memory settings..."
  DOTNET_GCConserveMemory=9 \
    dotnet build "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" \
    --no-restore --configuration Debug --verbosity quiet -maxcpucount:1 \
    || echo "[identity] Build error — will run from cached binary"
  echo "[tenant] Building Tenant.Api with conservative memory settings..."
  DOTNET_GCConserveMemory=9 \
    dotnet build "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj" \
    --no-restore --configuration Debug --verbosity quiet -maxcpucount:1 \
    || echo "[tenant] Build error — will run from cached binary"

  echo "[gateway] Starting Gateway on :5010..."
  ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://0.0.0.0:5010 \
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
  sleep 5

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
    DOTNET_GCConserveMemory=9 \
    dotnet run --no-build --project "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj" &
  sleep 5

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
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    ASPNETCORE_URLS=http://0.0.0.0:5035 \
    dotnet run --no-build --project "$ROOT/apps/services/xenia/Xenia.Api/Xenia.Api.csproj" &
  sleep 3
  ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_GCConserveMemory=9 \
    Authentication__Jwt__SymmetricKey="${Jwt__SigningKey:-dev-only-signing-key-minimum-32-chars-long!}" \
    dotnet run --no-build --project "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" &
  wait
) &
PID_DOTNET=$!

echo "[artifacts] Starting on :5020"
(
  cd "$ROOT/artifacts/api-server"
  ARTIFACTS_PORT=5020 NODE_ENV=development \
    node_modules/.bin/ts-node-dev --respawn --transpile-only src/server.ts
) &
PID_ARTIFACTS=$!

cleanup() {
  kill "$PID_DOTNET" "$PID_ARTIFACTS" 2>/dev/null || true
  wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait "$PID_DOTNET" "$PID_ARTIFACTS"
