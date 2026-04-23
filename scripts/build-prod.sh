#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Resolve the Next.js JS entrypoint (NOT the .bin/next shell wrapper —
# that file is a bash script and `node <bash-script>` blows up with
# "SyntaxError: missing ) after argument list", which silently failed
# every production build.
NEXT_BIN=""
for candidate in \
  "$ROOT/node_modules/next/dist/bin/next" \
  "$(npm root)/next/dist/bin/next" \
  "$(npm root -g 2>/dev/null)/next/dist/bin/next"; do
  if [ -f "$candidate" ]; then
    NEXT_BIN="$candidate"
    break
  fi
done

if [ -z "$NEXT_BIN" ]; then
  echo "ERROR: Cannot find next JS entrypoint. Installing next..."
  npm install next@15.2.9
  NEXT_BIN="$ROOT/node_modules/next/dist/bin/next"
fi

# Sanity-check: must start with the node shebang, never a bash one.
if ! head -n 1 "$NEXT_BIN" | grep -q '^#!/usr/bin/env node'; then
  echo "ERROR: $NEXT_BIN does not look like the Next.js JS entrypoint."
  exit 1
fi

echo "Using next binary: $NEXT_BIN"

echo "====== Building web app ======"
cd "$ROOT/apps/web"
rm -rf .next
NEXT_PUBLIC_ENV=production NEXT_PUBLIC_TENANT_CODE= GATEWAY_URL=http://127.0.0.1:5010 node "$NEXT_BIN" build

echo "====== Building control center ======"
# Deduplicate React: control-center has its own node_modules/react which creates
# a second React instance at SSR time, causing "useContext null" prerender failures.
# Replace with symlinks to the root pnpm store so both CC and react-dom share one copy.
PNPM_REACT="$ROOT/node_modules/.pnpm/react@18.3.1/node_modules/react"
PNPM_REACT_DOM="$ROOT/node_modules/.pnpm/react-dom@18.3.1_react@18.3.1/node_modules/react-dom"
CC_NM="$ROOT/apps/control-center/node_modules"
if [ -d "$PNPM_REACT" ] && [ ! -L "$CC_NM/react" ]; then
  rm -rf "$CC_NM/react"
  ln -s "$PNPM_REACT" "$CC_NM/react"
  echo "[dedup] Linked control-center/node_modules/react → pnpm store"
fi
if [ -d "$PNPM_REACT_DOM" ] && [ ! -L "$CC_NM/react-dom" ]; then
  rm -rf "$CC_NM/react-dom"
  ln -s "$PNPM_REACT_DOM" "$CC_NM/react-dom"
  echo "[dedup] Linked control-center/node_modules/react-dom → pnpm store"
fi
cd "$ROOT/apps/control-center"
rm -rf .next
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
  # Flow lives in its own solution — restore its packages separately so
  # the test-project dependencies don't block the service build.
  dotnet restore "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --verbosity minimal 2>&1 || true

  build_service "Gateway"       "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"
  build_service "Identity"      "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
  build_service "Fund"          "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
  build_service "CareConnect"   "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj"
  build_service "Documents"     "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj"
  build_service "Audit"         "$ROOT/apps/services/audit/PlatformAuditEventService.csproj"
  build_service "Notifications" "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj"
  build_service "Liens"         "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj"
  build_service "Flow API"      "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj"
  build_service "Monitoring"    "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj"
  build_service "Task"          "$ROOT/apps/services/task/Task.Api/Task.Api.csproj"
  build_service "Tenant"        "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj"

  if [ "$DOTNET_FAIL" -gt 0 ]; then
    echo "[dotnet] WARNING: $DOTNET_FAIL service(s) failed to build"
  else
    echo "[dotnet] All services built successfully"
  fi
else
  echo "[dotnet] WARNING: dotnet SDK not found — .NET services will not be available"
fi

echo "====== Cleaning up to reduce image size ======"

echo "[cleanup] Removing pnpm content-addressable store..."
rm -rf "$ROOT/.local/share/pnpm" 2>/dev/null || true

echo "[cleanup] Removing NuGet package cache..."
rm -rf "$ROOT/.local/share/NuGet" 2>/dev/null || true

echo "[cleanup] Removing Replit agent state..."
rm -rf "$ROOT/.local/state/replit" 2>/dev/null || true

echo "[cleanup] Removing .NET obj directories..."
find "$ROOT/apps" -type d -name obj -exec rm -rf {} + 2>/dev/null || true
find "$ROOT/shared" -type d -name obj -exec rm -rf {} + 2>/dev/null || true

echo "[cleanup] Removing .NET Debug build artifacts..."
find "$ROOT/apps" -path "*/bin/Debug" -type d -exec rm -rf {} + 2>/dev/null || true
find "$ROOT/shared" -path "*/bin/Debug" -type d -exec rm -rf {} + 2>/dev/null || true

echo "[cleanup] Removing archived files..."
rm -rf "$ROOT/_archived" 2>/dev/null || true

echo "[cleanup] Removing analysis/exports/downloads..."
rm -rf "$ROOT/analysis" 2>/dev/null || true
rm -rf "$ROOT/exports" 2>/dev/null || true
rm -rf "$ROOT/downloads" 2>/dev/null || true

echo "[cleanup] Removing attached_assets..."
rm -rf "$ROOT/attached_assets" 2>/dev/null || true

echo "[cleanup] Removing test bin/obj..."
find "$ROOT" -path "*Tests/bin" -type d -exec rm -rf {} + 2>/dev/null || true
find "$ROOT" -path "*Tests/obj" -type d -exec rm -rf {} + 2>/dev/null || true

echo "[cleanup] Removing git history (not needed at runtime)..."
rm -rf "$ROOT/.git" 2>/dev/null || true

echo "[cleanup] Removing workflow logs and temp files..."
rm -rf "$ROOT/.local/state/workflow-logs" 2>/dev/null || true
rm -rf "$ROOT/.local/state/scribe" 2>/dev/null || true

echo "[cleanup] Done"

echo "====== Build complete ======"
