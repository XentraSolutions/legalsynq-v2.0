#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NODE="/nix/store/51gywl5jn4nna7al9waj142pw4vfhy0k-nodejs-22.19.0/bin/node"

echo "====== LegalSynq dev startup ======"

# Start Next.js immediately — port 5000 must open for the preview pane
echo "[web] Starting Next.js on :5000"
(cd "$ROOT/apps/web" && GATEWAY_URL=http://localhost:5010 exec "$NODE" "$ROOT/node_modules/.bin/next" dev -p 5000) &
PID_WEB=$!

# Start Control Center — port 5004
echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 exec "$NODE" "$ROOT/node_modules/.bin/next" dev -p 5004) &
PID_CC=$!

# Restore, build, and start .NET services all in background
(
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity quiet
  dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Debug --verbosity quiet
  dotnet run --no-build --project "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5007 dotnet run --no-build --project "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" &
  dotnet run --no-build --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
  wait
) &
PID_DOTNET=$!

# Start notifications service — port 5008
echo "[notifications] Starting on :5008"
(
  cd "$ROOT/apps/services/notifications"
  PORT=5008 NODE_ENV=development \
    node_modules/.bin/ts-node-dev --respawn --transpile-only src/server.ts
) &
PID_NOTIF=$!

# Start notifications provider-health worker (long-running, uses setInterval — respawn is correct)
echo "[notifications:worker] Starting provider-health worker"
(
  cd "$ROOT/apps/services/notifications"
  NODE_ENV=development \
    node_modules/.bin/ts-node-dev --respawn --transpile-only src/workers/provider-health.worker.ts
) &
PID_NOTIF_WORKER=$!

# Start notifications dispatch worker (stub — exits immediately, no respawn to avoid restart loop)
echo "[notifications:worker] Starting dispatch worker (stub)"
(
  cd "$ROOT/apps/services/notifications"
  NODE_ENV=development \
    node_modules/.bin/ts-node --transpile-only src/workers/notification.worker.ts
) || true &

cleanup() {
    kill "$PID_WEB" "$PID_CC" "$PID_DOTNET" "$PID_NOTIF" "$PID_NOTIF_WORKER" 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait "$PID_WEB" "$PID_CC" "$PID_DOTNET" "$PID_NOTIF" "$PID_NOTIF_WORKER"
