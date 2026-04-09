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
(cd "$ROOT/apps/web" && NEXT_PUBLIC_ENV=production NEXT_PUBLIC_TENANT_CODE= GATEWAY_URL=http://localhost:5010 node "$NEXT_BIN" start -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting prod proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 node "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 node "$NEXT_BIN" start -p 5004) &
PID_CC=$!

echo "[dotnet] Starting .NET services"
launch_svc() {
  local name="$1" project="$2"
  shift 2
  local dll_dir
  dll_dir="$(dirname "$project")/bin/Release/net8.0"
  local dll_name
  dll_name="$(basename "$project" .csproj).dll"
  if [ -f "$dll_dir/$dll_name" ]; then
    (cd "$(dirname "$project")" && "$@" dotnet run --no-build --no-launch-profile --configuration Release) &
    echo "[dotnet] $name launched (pid $!)"
  else
    echo "[dotnet] $name SKIPPED — binary not found"
  fi
}

if command -v dotnet &>/dev/null; then
  (
    set +e
    export ASPNETCORE_ENVIRONMENT=Production
    GATEWAY_DLL="$ROOT/apps/gateway/Gateway.Api/bin/Release/net8.0/Gateway.Api.dll"
    IDENTITY_DLL="$ROOT/apps/services/identity/Identity.Api/bin/Release/net8.0/Identity.Api.dll"

    if [ ! -f "$GATEWAY_DLL" ] || [ ! -f "$IDENTITY_DLL" ]; then
      echo "[dotnet] Critical binaries missing — building now..."
      dotnet restore "$ROOT/LegalSynq.sln" --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
    else
      echo "[dotnet] Pre-built binaries found — skipping build"
    fi

    echo "[dotnet] Launching services..."
    launch_svc "Identity API" "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
    launch_svc "Fund API"     "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
    launch_svc "CareConnect"  "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj"
    launch_svc "Documents"    "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj"
    launch_svc "Audit"        "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" \
      env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5007
    launch_svc "Notifications" "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj"
    launch_svc "Gateway"      "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"

    echo "[dotnet] Service launch complete"
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

ALL_PIDS="$PID_WEB $PID_PROXY $PID_CC $PID_ARTIFACTS"
[ -n "$PID_DOTNET" ] && ALL_PIDS="$ALL_PIDS $PID_DOTNET"

cleanup() {
    kill $ALL_PIDS 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait $ALL_PIDS
