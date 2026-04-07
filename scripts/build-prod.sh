#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

NEXT_BIN="$(command -v next 2>/dev/null || true)"
if [ -z "$NEXT_BIN" ]; then
  for candidate in \
    "$ROOT/node_modules/.bin/next" \
    "$ROOT/node_modules/next/dist/bin/next" \
    "$(npm root)/next/dist/bin/next" \
    "$(npm root -g 2>/dev/null)/next/dist/bin/next"; do
    if [ -f "$candidate" ]; then
      NEXT_BIN="$candidate"
      break
    fi
  done
fi

if [ -z "$NEXT_BIN" ]; then
  echo "ERROR: Cannot find next binary. Installing next..."
  npm install next@15.2.9
  NEXT_BIN="$(npm root)/next/dist/bin/next"
fi

echo "Using next binary: $NEXT_BIN"

echo "====== Building web app ======"
cd "$ROOT/apps/web"
node "$NEXT_BIN" build

echo "====== Building control center ======"
cd "$ROOT/apps/control-center"
node "$NEXT_BIN" build

echo "====== Building .NET services ======"
cd "$ROOT"
if command -v dotnet &>/dev/null; then
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity minimal
  dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Release --verbosity minimal
  dotnet publish "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" \
    --configuration Release \
    --output "$ROOT/apps/services/audit/bin/Release/net8.0/" \
    --no-restore --verbosity quiet
  echo "[dotnet] Build succeeded"
else
  echo "[dotnet] WARNING: dotnet SDK not found — .NET services will not be available"
fi

echo "====== Build complete ======"
