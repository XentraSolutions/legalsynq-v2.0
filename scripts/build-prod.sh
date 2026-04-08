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
NEXT_PUBLIC_ENV=production NEXT_PUBLIC_TENANT_CODE= GATEWAY_URL=http://localhost:5010 node "$NEXT_BIN" build

echo "====== Building control center ======"
cd "$ROOT/apps/control-center"
node "$NEXT_BIN" build

echo "====== Building .NET services ======"
cd "$ROOT"
if command -v dotnet &>/dev/null; then
  DOTNET_FAIL=0

  build_service() {
    local name="$1"
    local project="$2"
    echo "[dotnet] Building $name..."
    if dotnet build "$project" --configuration Release --verbosity minimal 2>&1; then
      echo "[dotnet] $name — OK"
    else
      echo "[dotnet] $name — FAILED (non-fatal)"
      DOTNET_FAIL=$((DOTNET_FAIL + 1))
    fi
  }

  echo "[dotnet] Restoring packages..."
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity minimal 2>&1 || true

  build_service "Gateway"     "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"
  build_service "Identity"    "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
  build_service "Fund"        "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
  build_service "CareConnect" "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj"
  build_service "Documents"   "$ROOT/apps/services/documents-dotnet/Documents.Api/Documents.Api.csproj"
  build_service "Audit"       "$ROOT/apps/services/audit/PlatformAuditEventService.csproj"

  if [ "$DOTNET_FAIL" -gt 0 ]; then
    echo "[dotnet] WARNING: $DOTNET_FAIL service(s) failed to build"
  else
    echo "[dotnet] All services built successfully"
  fi
else
  echo "[dotnet] WARNING: dotnet SDK not found — .NET services will not be available"
fi

echo "====== Build complete ======"
