#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "====== LegalSynq production startup ======"

NEXT_INTERNAL_PORT=3050
echo "[web] Starting Next.js on :$NEXT_INTERNAL_PORT (internal)"
(cd "$ROOT/apps/web" && GATEWAY_URL=http://localhost:5010 node "$ROOT/node_modules/next/dist/bin/next" start -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting prod proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 node "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 node "$ROOT/node_modules/next/dist/bin/next" start -p 5004) &
PID_CC=$!

(
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity quiet
  dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Release --verbosity quiet
  dotnet run --no-build --configuration Release --project "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" &
  dotnet run --no-build --configuration Release --project "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" &
  dotnet run --no-build --configuration Release --project "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://0.0.0.0:5007 dotnet run --no-build --configuration Release --project "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" &
  ASPNETCORE_ENVIRONMENT=Production dotnet run --no-build --configuration Release --project "$ROOT/apps/services/documents-dotnet/Documents.Api/Documents.Api.csproj" &
  dotnet run --no-build --configuration Release --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
  wait
) &
PID_DOTNET=$!

echo "[artifacts] Starting on :5020"
(
  cd "$ROOT/artifacts/api-server"
  ARTIFACTS_PORT=5020 NODE_ENV=production \
    node_modules/.bin/ts-node --transpile-only src/server.ts
) &
PID_ARTIFACTS=$!

echo "[notifications] Starting on :5008"
(
  cd "$ROOT/apps/services/notifications"
  PORT=5008 NODE_ENV=production \
    node_modules/.bin/ts-node --transpile-only src/server.ts
) &
PID_NOTIF=$!

echo "[notifications:worker] Starting provider-health worker"
(
  cd "$ROOT/apps/services/notifications"
  NODE_ENV=production \
    node_modules/.bin/ts-node --transpile-only src/workers/provider-health.worker.ts
) &
PID_NOTIF_WORKER=$!

echo "[notifications:worker] Starting dispatch worker"
(
  cd "$ROOT/apps/services/notifications"
  NODE_ENV=production \
    node_modules/.bin/ts-node --transpile-only src/workers/notification.worker.ts
) || true &

echo "[notifications:worker] Starting status-sync worker"
(
  cd "$ROOT/apps/services/notifications"
  NODE_ENV=production \
    node_modules/.bin/ts-node --transpile-only src/workers/status-sync.worker.ts
) &
PID_STATUS_SYNC=$!

cleanup() {
    kill "$PID_WEB" "$PID_PROXY" "$PID_CC" "$PID_DOTNET" "$PID_ARTIFACTS" "$PID_NOTIF" "$PID_NOTIF_WORKER" "$PID_STATUS_SYNC" 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait "$PID_WEB" "$PID_PROXY" "$PID_CC" "$PID_DOTNET" "$PID_ARTIFACTS" "$PID_NOTIF" "$PID_NOTIF_WORKER" "$PID_STATUS_SYNC"
