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
  "Intake.Api/Intake.Api.csproj"
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

# ── Monitoring retry wrapper ───────────────────────────────────────────────────
# The monitoring service is started inside a bash retry subshell in run-dev.sh.
# Killing the dotnet process alone is not enough — the bash wrapper catches the
# exit (via || true), sleeps 15 s, and relaunches the service.
# We kill the sed pipeline partner ([monitoring] prefix) and then walk up to its
# bash parent (the retry subshell) and kill that too.
_monitoring_sed_pids=$(pgrep -f '\[monitoring\]' 2>/dev/null || true)
if [ -n "$_monitoring_sed_pids" ]; then
  for _pid in $_monitoring_sed_pids; do
    _ppid=$(ps -o ppid= -p "$_pid" 2>/dev/null | tr -d ' ' || true)
    echo "[monitoring] Stopping retry wrapper (sed pid: $_pid, bash pid: ${_ppid:-?})"
    kill "$_pid" 2>/dev/null || true
    [ -n "$_ppid" ] && [ "$_ppid" -gt 1 ] && kill "$_ppid" 2>/dev/null || true
  done
else
  echo "[monitoring] No retry wrapper found (already stopped or not started)"
fi

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

# Force-kill any surviving monitoring retry wrapper (sed + bash parent).
_monitoring_sed_pids=$(pgrep -f '\[monitoring\]' 2>/dev/null || true)
for _pid in $_monitoring_sed_pids; do
  _ppid=$(ps -o ppid= -p "$_pid" 2>/dev/null | tr -d ' ' || true)
  kill -9 "$_pid" 2>/dev/null || true
  [ -n "$_ppid" ] && [ "$_ppid" -gt 1 ] && kill -9 "$_ppid" 2>/dev/null || true
done

for port in "${NODE_PORTS[@]}"; do
  pids=$(lsof -ti tcp:"$port" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "[node]   Force-killing :$port (pids: $pids)"
    kill -9 $pids 2>/dev/null || true
  fi
done

echo "====== All dev services stopped ======"
