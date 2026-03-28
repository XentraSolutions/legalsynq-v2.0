#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "======================================="
echo "  LegalSynq — starting all services"
echo "======================================="
echo ""

echo "Restoring dependencies..."
dotnet restore "$ROOT/LegalSynq.sln"

echo "Building solution..."
dotnet build "$ROOT/LegalSynq.sln" --no-restore --configuration Debug

echo ""
echo "Starting services..."
echo "  Identity → http://localhost:5001"
echo "  Fund     → http://localhost:5002"
echo "  Gateway  → http://localhost:5000"
echo ""

dotnet run --no-build --project "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" &
PID_IDENTITY=$!

dotnet run --no-build --project "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" &
PID_FUND=$!

dotnet run --no-build --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
PID_GATEWAY=$!

cleanup() {
    echo ""
    echo "Stopping all services..."
    kill "$PID_IDENTITY" "$PID_FUND" "$PID_GATEWAY" 2>/dev/null || true
    wait "$PID_IDENTITY" "$PID_FUND" "$PID_GATEWAY" 2>/dev/null || true
    echo "All services stopped."
}

trap cleanup EXIT INT TERM

wait "$PID_IDENTITY" "$PID_FUND" "$PID_GATEWAY"
