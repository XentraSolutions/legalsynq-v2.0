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

# ── Health-check probe helper ──────────────────────────────────────────────────
# Polls <scheme>://<host>:<port><path> in the background for up to $deadline
# seconds.  Logs a clear WARNING if the service never responds.
# Used for both .NET and Node.js services.
_probe_svc() {
  local label="$1" port="$2" path="$3" deadline="${4:-90}"
  (
    echo "[$label] Waiting for $path on :$port..."
    local elapsed=0
    while [ "$elapsed" -lt "$deadline" ]; do
      if curl -sf "http://127.0.0.1:${port}${path}" >/dev/null 2>&1; then
        echo "[$label] $path healthy after ${elapsed}s"
        exit 0
      fi
      sleep 5
      elapsed=$((elapsed + 5))
    done
    echo "[$label] WARNING: $path on :$port did not respond within ${deadline}s — $label may be unhealthy"
  ) &
}

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
# shellcheck source=scripts/lib/dotnet-helpers.sh
source "$ROOT/scripts/lib/dotnet-helpers.sh"

if command -v dotnet &>/dev/null; then
  (
    set +e
    export ASPNETCORE_ENVIRONMENT=Production

    # ── Single source of truth for all .NET services ─────────────────────────
    # Add a new service by appending its .csproj path here.  The DLL check,
    # build step, and post-build verification all derive from this list
    # automatically — no other section needs updating for the build pipeline.
    # Services are listed in launch order: backend services first, Gateway last
    # so it only begins routing once all upstream services are started.
    # New services should be inserted before Gateway.Api.csproj.
    BUILD_PROJECTS=(
      "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
      "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
      "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj"
      "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj"
      "$ROOT/apps/services/audit/PlatformAuditEventService.csproj"
      "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj"
      "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj"
      "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj"
      "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"
    )

    # Derives the expected Release output DLL from a .csproj path.
    # Uses the same convention as the launch_svc helper above.
    dll_for_csproj() {
      local csproj="$1"
      echo "$(dirname "$csproj")/bin/Release/net8.0/$(basename "$csproj" .csproj).dll"
    }

    need_build=0
    for csproj in "${BUILD_PROJECTS[@]}"; do
      dll="$(dll_for_csproj "$csproj")"
      if [ ! -f "$dll" ]; then
        echo "[dotnet] Missing binary: $dll"
        need_build=1
      fi
    done

    if [ "$need_build" -eq 1 ]; then
      echo "[dotnet] One or more binaries missing — building all services now..."
      for csproj in "${BUILD_PROJECTS[@]}"; do
        svc_name="$(basename "$csproj" .csproj)"
        echo "[dotnet] Building $svc_name..."
        dotnet build "$csproj" --configuration Release --verbosity minimal \
          || { echo "[dotnet] ERROR: $svc_name build failed — aborting"; exit 1; }
      done

      for csproj in "${BUILD_PROJECTS[@]}"; do
        dll="$(dll_for_csproj "$csproj")"
        if [ ! -f "$dll" ]; then
          echo "[dotnet] ERROR: binary not produced after build: $dll — aborting"
          exit 1
        fi
      done
    else
      echo "[dotnet] All service binaries present — skipping build"
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
    SKIP_FLOW=0
    # Flow.Infrastructure.DependencyInjection reads FLOW_DB_CONNECTION_STRING
    # first, then ConnectionStrings:FlowDb. Accept either form here so the
    # check mirrors the actual runtime resolution order.
    if [ -z "${FLOW_DB_CONNECTION_STRING:-}" ] && [ -z "${ConnectionStrings__FlowDb:-}" ]; then
      echo "[flow] WARNING: neither FLOW_DB_CONNECTION_STRING nor ConnectionStrings__FlowDb is set — Flow will be skipped"
      SKIP_FLOW=1
    fi
    if [ -z "${ServiceToken__SigningKey:-}" ]; then
      echo "[flow] WARNING: ServiceToken__SigningKey is not set — Flow will be skipped (failFastIfMissingSecret is true in Production)"
      SKIP_FLOW=1
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
    # Derived from BUILD_PROJECTS — every service that was built is launched.
    # Per-service env vars (e.g. ASPNETCORE_URLS) are set via a case statement;
    # the catch-all (*) ensures any new entry in BUILD_PROJECTS is launched
    # automatically even if it has no special configuration.
    # SVC_PIDS / SVC_NAMES are populated here and consumed by the crash-monitor
    # immediately below the loop.
    SVC_PIDS=()
    SVC_NAMES=()
    for csproj in "${BUILD_PROJECTS[@]}"; do
      svc_name="$(basename "$csproj" .csproj)"
      case "$svc_name" in
        PlatformAuditEventService)
          _svc_label="Audit"
          launch_svc "$_svc_label"    "$csproj" env ASPNETCORE_URLS=http://0.0.0.0:5007 ;;
        Notifications.Api)
          _svc_label="Notifications"
          launch_svc "$_svc_label"    "$csproj" env ASPNETCORE_URLS=http://0.0.0.0:5008 ;;
        Flow.Api)
          _svc_label="Flow API"
          if [ "$SKIP_FLOW" -eq 1 ]; then
            echo "[flow] Skipping Flow API launch due to missing required env vars"
            continue
          fi
          launch_svc "$_svc_label"    "$csproj" env ASPNETCORE_URLS=http://0.0.0.0:5012 ;;
        Gateway.Api)     _svc_label="Gateway";      launch_svc "$_svc_label" "$csproj" ;;
        Identity.Api)    _svc_label="Identity API"; launch_svc "$_svc_label" "$csproj" ;;
        Fund.Api)        _svc_label="Fund API";     launch_svc "$_svc_label" "$csproj" ;;
        CareConnect.Api) _svc_label="CareConnect";  launch_svc "$_svc_label" "$csproj" ;;
        Documents.Api)   _svc_label="Documents";    launch_svc "$_svc_label" "$csproj" ;;
        Liens.Api)       _svc_label="Liens";        launch_svc "$_svc_label" "$csproj" ;;
        *)               _svc_label="$svc_name";    launch_svc "$_svc_label" "$csproj" ;;
      esac
      # $! is the PID of the dotnet process just backgrounded by launch_svc
      SVC_PIDS+=("$!")
      SVC_NAMES+=("$_svc_label")
    done

    echo "[dotnet] Service launch complete"

    # ── Fast crash-detection ──────────────────────────────────────────────────
    # Poll every 0.5 s for up to 5 s.  Runs entirely in the background so
    # healthy startups are not slowed down at all.  A crashed service is
    # reported within 0.5 s of its exit — no fixed wait required.
    (
      window=10  # 10 × 0.5 s = 5 s observation window
      for ((tick = 1; tick <= window; tick++)); do
        sleep 0.5
        for idx in "${!SVC_PIDS[@]}"; do
          pid="${SVC_PIDS[$idx]}"
          name="${SVC_NAMES[$idx]}"
          if ! kill -0 "$pid" 2>/dev/null; then
            echo "[dotnet] ERROR: ${name} (pid ${pid}) crashed $((tick * 500))ms after launch"
            exit 1
          fi
        done
      done
    ) &

    # ── Health-check probes for all .NET services ─────────────────────────
    # Each probe polls its /health (or /healthz for Flow) endpoint for up to
    # 90 seconds after launch (.NET cold-start + build can take that long).
    # All probes run in the background so they do not block each other or the
    # wait below.  A clear WARNING is logged if a service does not respond
    # within the deadline.
    # _probe_svc is defined at the top of this script and inherited here.

    _probe_svc "Identity"      5001 /health
    _probe_svc "Fund"          5002 /health
    _probe_svc "CareConnect"   5003 /health
    _probe_svc "Documents"     5006 /health
    _probe_svc "Audit"         5007 /health
    _probe_svc "Notifications" 5008 /health
    _probe_svc "Liens"         5009 /health
    _probe_svc "Gateway"       5010 /health
    _probe_svc "Flow"          5012 /healthz

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

# ── Health-check probes for all Node.js services ──────────────────────────────
# Each probe polls a liveness endpoint in the background for up to 60 seconds.
# Node.js processes (Next.js, Express) typically start well under 30s, so a
# 60-second deadline surfaces failures faster than the 90s used for .NET.
# A clear WARNING is logged if the service does not respond within the deadline.
_probe_svc "Web"       3050 /api/health 60
_probe_svc "Proxy"     5000 /health     60
_probe_svc "CC"        5004 /api/health 60
_probe_svc "Artifacts" 5020 /api/health 60

ALL_PIDS="$PID_WEB $PID_PROXY $PID_CC $PID_ARTIFACTS"
[ -n "$PID_DOTNET" ] && ALL_PIDS="$ALL_PIDS $PID_DOTNET"

cleanup() {
    kill $ALL_PIDS 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

# Wait for all background services and propagate failures visibly.
# PID_DOTNET is appended last to ALL_PIDS, so `wait` returns its exit code
# when it is the last process to be waited on.  We capture that code and emit
# a clear diagnostic so operators do not have to guess why the script failed.
set +e
wait $ALL_PIDS
_startup_ec=$?
set -e
if [ "$_startup_ec" -ne 0 ]; then
  if [ -n "$PID_DOTNET" ]; then
    echo "[dotnet] ERROR: The .NET services subshell exited with code $_startup_ec — one or more .NET services crashed at launch. Review the output above for details."
  else
    echo "[startup] ERROR: A service exited with code $_startup_ec. Review the output above for details."
  fi
  exit "$_startup_ec"
fi
