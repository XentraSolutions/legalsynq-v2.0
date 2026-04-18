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
(cd "$ROOT/apps/web" && NEXT_PUBLIC_ENV=production NEXT_PUBLIC_TENANT_CODE= GATEWAY_URL=http://127.0.0.1:5010 node "$NEXT_BIN" start -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting prod proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 node "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://127.0.0.1:5010 node "$NEXT_BIN" start -p 5004) &
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
    LIENS_DLL="$ROOT/apps/services/liens/Liens.Api/bin/Release/net8.0/Liens.Api.dll"
    FLOW_DLL="$ROOT/apps/services/flow/backend/src/Flow.Api/bin/Release/net8.0/Flow.Api.dll"

    if [ ! -f "$GATEWAY_DLL" ] || [ ! -f "$IDENTITY_DLL" ] || [ ! -f "$LIENS_DLL" ] || [ ! -f "$FLOW_DLL" ]; then
      echo "[dotnet] Critical binaries missing — building now..."
      dotnet restore "$ROOT/LegalSynq.sln" --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      dotnet build "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj" --configuration Release --verbosity minimal 2>&1 || true
      # Flow has its own solution — restore and build are fail-fast so a
      # compile error does not silently produce a missing binary.
      dotnet restore "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --verbosity minimal \
        || { echo "[dotnet] ERROR: Flow restore failed — aborting"; exit 1; }
      dotnet build "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --configuration Release --verbosity minimal \
        || { echo "[dotnet] ERROR: Flow build failed — aborting"; exit 1; }
      if [ ! -f "$FLOW_DLL" ]; then
        echo "[dotnet] ERROR: Flow binary not produced after build — aborting"
        exit 1
      fi
    else
      echo "[dotnet] Pre-built binaries found — skipping build"
    fi

    # ── Flow env-var pre-flight ───────────────────────────────────────────
    # Hard-fail on required keys so the operator gets a clear error message
    # before services are launched, rather than an opaque crash post-start.
    #
    # FLOW_DB_CONNECTION_STRING — required; without it, the app falls back to
    #   a localhost address that never works in a deployed environment.
    # ServiceToken__SigningKey — required in Production; Flow's Program.cs
    #   calls AddServiceTokenBearer with failFastIfMissingSecret:true outside
    #   Development, so startup crashes if this key is absent.
    flow_missing_required=0
    # Flow.Infrastructure.DependencyInjection reads FLOW_DB_CONNECTION_STRING
    # first, then ConnectionStrings:FlowDb. Accept either form here so the
    # check mirrors the actual runtime resolution order.
    if [ -z "${FLOW_DB_CONNECTION_STRING:-}" ] && [ -z "${ConnectionStrings__FlowDb:-}" ]; then
      echo "[flow] ERROR: neither FLOW_DB_CONNECTION_STRING nor ConnectionStrings__FlowDb is set — Flow cannot start without a database connection"
      flow_missing_required=1
    fi
    if [ -z "${ServiceToken__SigningKey:-}" ]; then
      echo "[flow] ERROR: ServiceToken__SigningKey is not set — Flow will crash on startup in Production (failFastIfMissingSecret is true)"
      flow_missing_required=1
    fi
    if [ "$flow_missing_required" -ne 0 ]; then
      exit 1
    fi
    # JWT keys are optional — Program.cs registers a no-op JWT bearer when
    # Jwt:SigningKey is absent (user tokens simply won't validate), so these
    # are warnings rather than hard failures.
    for key in "Jwt__SigningKey" "Jwt__Issuer" "Jwt__Audience"; do
      if [ -z "${!key:-}" ]; then
        echo "[flow] WARNING: $key is not set — user-token auth will be disabled"
      fi
    done
    # CORS is not required for service startup but an empty AllowedOrigins
    # means browser clients will be blocked by CORS policy.
    if [ -z "${Cors__AllowedOrigins:-}" ]; then
      echo "[flow] WARNING: Cors__AllowedOrigins is not set — browser cross-origin requests to Flow will be rejected"
    fi

    echo "[dotnet] Launching services..."
    launch_svc "Identity API" "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
    launch_svc "Fund API"     "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
    launch_svc "CareConnect"  "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj"
    launch_svc "Documents"    "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj"
    launch_svc "Audit"        "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" \
      env ASPNETCORE_URLS=http://0.0.0.0:5007
    launch_svc "Notifications" "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj" \
      env ASPNETCORE_URLS=http://0.0.0.0:5008
    launch_svc "Liens"        "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj"
    launch_svc "Flow API"     "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" \
      env ASPNETCORE_URLS=http://0.0.0.0:5012
    launch_svc "Gateway"      "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"

    echo "[dotnet] Service launch complete"

    # ── Flow health-check probe ───────────────────────────────────────────
    # Wait up to 90 seconds for Flow /healthz to respond, then log the
    # result. Services run as long-lived processes, so this probe runs
    # in the background and does not block the wait below.
    (
      echo "[flow] Waiting for /healthz on :5012..."
      deadline=90
      elapsed=0
      while [ "$elapsed" -lt "$deadline" ]; do
        if curl -sf http://127.0.0.1:5012/healthz >/dev/null 2>&1; then
          echo "[flow] /healthz healthy after ${elapsed}s"
          exit 0
        fi
        sleep 5
        elapsed=$((elapsed + 5))
      done
      echo "[flow] WARNING: /healthz on :5012 did not respond within ${deadline}s — Flow may be unhealthy"
    ) &

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
