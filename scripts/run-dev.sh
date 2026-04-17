#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NODE="/nix/store/51gywl5jn4nna7al9waj142pw4vfhy0k-nodejs-22.19.0/bin/node"

echo "====== LegalSynq dev startup ======"

# Start Next.js on an internal port; the proxy on :5000 gates requests
# until the cold-compile race condition is resolved (HTTP 200 on /login).
NEXT_INTERNAL_PORT=3050
echo "[web] Starting Next.js on :$NEXT_INTERNAL_PORT (internal)"
(cd "$ROOT/apps/web" && GATEWAY_URL=http://localhost:5010 exec "$NODE" "$ROOT/node_modules/next/dist/bin/next" dev -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting dev proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 "$NODE" "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

# Start Control Center — port 5004
echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 exec "$NODE" "$ROOT/node_modules/next/dist/bin/next" dev -p 5004) &
PID_CC=$!

# Restore, build, and start .NET services all in background
(
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity quiet
  dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Debug --verbosity quiet
  dotnet build "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" --configuration Debug --verbosity quiet
  dotnet run --no-build --project "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5007 dotnet run --no-build --project "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" &
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/comms/Comms.Api/Comms.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
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
