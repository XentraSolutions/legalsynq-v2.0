#!/usr/bin/env bash
# stop-backend-dev.sh — Terminate LegalSynq backend dev services only.

set -uo pipefail

echo "====== LegalSynq backend dev shutdown ======"

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
  "Xenia.Api/Xenia.Api.csproj"
)

for proj in "${DOTNET_PROJECTS[@]}"; do
  pids=$(pgrep -f "$proj" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "[dotnet] Stopping $proj (pids: $pids)"
    kill $pids 2>/dev/null || true
  fi
done

_monitoring_sed_pids=$(pgrep -f '\[monitoring\]' 2>/dev/null || true)
if [ -n "$_monitoring_sed_pids" ]; then
  for _pid in $_monitoring_sed_pids; do
    _ppid=$(ps -o ppid= -p "$_pid" 2>/dev/null | tr -d ' ' || true)
    echo "[monitoring] Stopping retry wrapper (sed pid: $_pid, bash pid: ${_ppid:-?})"
    kill "$_pid" 2>/dev/null || true
    [ -n "$_ppid" ] && [ "$_ppid" -gt 1 ] && kill "$_ppid" 2>/dev/null || true
  done
fi

ARTIFACTS_PORT=5020
artifacts_pids=$(lsof -ti tcp:"$ARTIFACTS_PORT" 2>/dev/null || true)
if [ -n "$artifacts_pids" ]; then
  echo "[artifacts] Stopping process(es) on :$ARTIFACTS_PORT (pids: $artifacts_pids)"
  kill $artifacts_pids 2>/dev/null || true
fi

ts_node_pids=$(pgrep -f "ts-node-dev.*server.ts" 2>/dev/null || true)
if [ -n "$ts_node_pids" ]; then
  echo "[artifacts] Stopping ts-node-dev (pids: $ts_node_pids)"
  kill $ts_node_pids 2>/dev/null || true
fi

sleep 2

for proj in "${DOTNET_PROJECTS[@]}"; do
  pids=$(pgrep -f "$proj" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "[dotnet] Force-killing $proj (pids: $pids)"
    kill -9 $pids 2>/dev/null || true
  fi
done

_monitoring_sed_pids=$(pgrep -f '\[monitoring\]' 2>/dev/null || true)
for _pid in $_monitoring_sed_pids; do
  _ppid=$(ps -o ppid= -p "$_pid" 2>/dev/null | tr -d ' ' || true)
  kill -9 "$_pid" 2>/dev/null || true
  [ -n "$_ppid" ] && [ "$_ppid" -gt 1 ] && kill -9 "$_ppid" 2>/dev/null || true
done

artifacts_pids=$(lsof -ti tcp:"$ARTIFACTS_PORT" 2>/dev/null || true)
if [ -n "$artifacts_pids" ]; then
  echo "[artifacts] Force-killing :$ARTIFACTS_PORT (pids: $artifacts_pids)"
  kill -9 $artifacts_pids 2>/dev/null || true
fi

echo "====== Backend dev services stopped ======"
