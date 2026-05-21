#!/usr/bin/env bash
# stop-dev.sh — Terminate all LegalSynq dev services.
# Kills dotnet, Next.js, proxy, control-center, and artifacts processes
# started by run-dev.sh.

set -uo pipefail

echo "====== LegalSynq dev shutdown ======"

# ── .NET service project paths ────────────────────────────────────────────────
DOTNET_PROJECTS=(
  "Identity.Api/Identity.Api.csproj"
  "Fund.Api/Fund.Api.csproj"
  "CareConnect.Api/CareConnect.Api.csproj"
  "Liens.Api/Liens.Api.csproj"
  "PlatformAuditEventService.csproj"
  "Documents.Api/Documents.Api.csproj"
  "Notifications.Api/Notifications.Api.csproj"
  "Comms.Api/Comms.Api.csproj"
  "Flow.Api/Flow.Api.csproj"
  "Monitoring.Api/Monitoring.Api.csproj"
  "Reports.Api/Reports.Api.csproj"
  "Task.Api/Task.Api.csproj"
  "Tenant.Api/Tenant.Api.csproj"
  "Support.Api/Support.Api.csproj"
  "Commerce.Api/Commerce.Api.csproj"
  "Billing.Api/Billing.Api.csproj"
  "Gateway.Api/Gateway.Api.csproj"
)

# Kill any dotnet process whose command line references a known project file.
KILLED_DOTNET=0
for proj in "${DOTNET_PROJECTS[@]}"; do
  pids=$(pgrep -f "$proj" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "[dotnet] Stopping $proj (pids: $pids)"
    kill $pids 2>/dev/null || true
    KILLED_DOTNET=$((KILLED_DOTNET + 1))
  fi
done

# ── Ports used by Node / proxy / artifacts ────────────────────────────────────
NODE_PORTS=(3050 5000 5004 5020)

KILLED_NODE=0
for port in "${NODE_PORTS[@]}"; do
  pids=$(lsof -ti tcp:"$port" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "[node]   Stopping process(es) on :$port (pids: $pids)"
    kill $pids 2>/dev/null || true
    KILLED_NODE=$((KILLED_NODE + 1))
  fi
done

# ── dev-proxy.js ──────────────────────────────────────────────────────────────
pids=$(pgrep -f "dev-proxy.js" 2>/dev/null || true)
if [ -n "$pids" ]; then
  echo "[proxy]  Stopping dev-proxy.js (pids: $pids)"
  kill $pids 2>/dev/null || true
fi

# ── ts-node-dev (artifacts API) ───────────────────────────────────────────────
pids=$(pgrep -f "ts-node-dev.*server.ts" 2>/dev/null || true)
if [ -n "$pids" ]; then
  echo "[artifacts] Stopping ts-node-dev (pids: $pids)"
  kill $pids 2>/dev/null || true
fi

# Brief wait, then SIGKILL any survivors.
sleep 2

for proj in "${DOTNET_PROJECTS[@]}"; do
  pids=$(pgrep -f "$proj" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "[dotnet] Force-killing $proj (pids: $pids)"
    kill -9 $pids 2>/dev/null || true
  fi
done

for port in "${NODE_PORTS[@]}"; do
  pids=$(lsof -ti tcp:"$port" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "[node]   Force-killing :$port (pids: $pids)"
    kill -9 $pids 2>/dev/null || true
  fi
done

echo "====== All dev services stopped ======"
