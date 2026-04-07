#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

NEXT_BIN=""
for candidate in \
  "$ROOT/node_modules/.bin/next" \
  "$ROOT/node_modules/next/dist/bin/next" \
  "$(npm root 2>/dev/null)/next/dist/bin/next"; do
  if [ -f "$candidate" ]; then
    NEXT_BIN="$candidate"
    break
  fi
done
if [ -z "$NEXT_BIN" ]; then
  NEXT_BIN="$(which next 2>/dev/null || echo next)"
fi

echo "====== LegalSynq production startup ======"
echo "[next] Using: $NEXT_BIN"

NEXT_INTERNAL_PORT=3050
echo "[web] Starting Next.js on :$NEXT_INTERNAL_PORT (internal)"
(cd "$ROOT/apps/web" && GATEWAY_URL=http://localhost:5010 node "$NEXT_BIN" start -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting prod proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 node "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 node "$NEXT_BIN" start -p 5004) &
PID_CC=$!

echo "[dotnet] Starting .NET services"
if command -v dotnet &>/dev/null; then
  (
    set +e
    echo "[dotnet] Restoring and building..."
    dotnet restore "$ROOT/LegalSynq.sln" --verbosity minimal 2>&1
    dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Release --verbosity minimal 2>&1
    if [ $? -ne 0 ]; then
      echo "[dotnet] ERROR: Build failed — .NET services will not start"
      exit 1
    fi
    echo "[dotnet] Build succeeded, starting services..."
    dotnet run --no-build --configuration Release --project "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" &
    dotnet run --no-build --configuration Release --project "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" &
    dotnet run --no-build --configuration Release --project "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" &
    ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://0.0.0.0:5007 dotnet run --no-build --configuration Release --project "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" &
    ASPNETCORE_ENVIRONMENT=Production dotnet run --no-build --configuration Release --project "$ROOT/apps/services/documents-dotnet/Documents.Api/Documents.Api.csproj" &
    dotnet run --no-build --configuration Release --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
    echo "[dotnet] All .NET services launched"
    wait
  ) &
  PID_DOTNET=$!
else
  echo "[dotnet] WARNING: dotnet SDK not found — .NET services will not start"
  PID_DOTNET=""
fi

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

ALL_PIDS="$PID_WEB $PID_PROXY $PID_CC $PID_ARTIFACTS $PID_NOTIF $PID_NOTIF_WORKER $PID_STATUS_SYNC"
[ -n "$PID_DOTNET" ] && ALL_PIDS="$ALL_PIDS $PID_DOTNET"

cleanup() {
    kill $ALL_PIDS 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait $ALL_PIDS
