#!/bin/bash
set -e

echo "=== Post-merge setup ==="

echo "Installing frontend dependencies..."
# Cap Node.js heap to avoid SIGABRT in memory-constrained environment.
# Three-tier fallback: frozen → regular → ignore-scripts (packages already cached).
export NODE_OPTIONS="--max-old-space-size=512"

pnpm_install() {
  if pnpm install --frozen-lockfile 2>/dev/null; then
    return 0
  fi
  echo "  (frozen-lockfile failed, retrying without it...)"
  if pnpm install 2>/dev/null; then
    return 0
  fi
  echo "  (retry failed, falling back to --ignore-scripts...)"
  pnpm install --ignore-scripts
}

pnpm_install

echo "Verifying .NET SDK version..."
dotnet --version

echo "Building shared .NET libraries (net8.0 — buildable in this environment)..."
export DOTNET_GCConserveMemory=9
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1
export DOTNET_GCHeapHardLimit=419430400

build_shared() {
  local proj="$1"
  echo "  -> building $proj"
  local attempt=0
  while true; do
    attempt=$((attempt + 1))
    if dotnet restore "$proj" --verbosity quiet \
       && dotnet build "$proj" --no-restore --verbosity quiet -maxcpucount:1 -nodeReuse:false; then
      return 0
    fi
    if [ "$attempt" -ge 2 ]; then
      echo "  ERROR: $proj failed after $attempt attempt(s)" >&2
      return 1
    fi
    echo "  (attempt $attempt failed, retrying after 5s...)"
    sleep 5
  done
}

build_shared shared/contracts/Contracts/Contracts.csproj
build_shared shared/audit-client/LegalSynq.AuditClient/LegalSynq.AuditClient.csproj
build_shared shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj

# Service projects target net10.0. If the installed SDK supports net10.0, build them too.
SDK_VER=$(dotnet --version 2>/dev/null | cut -d. -f1)
if [ "$SDK_VER" -ge 10 ] 2>/dev/null; then
  echo "SDK >= 10 detected — building service projects..."

  build_service() {
    local proj="$1"
    echo "  -> building $proj"
    local attempt=0
    while true; do
      attempt=$((attempt + 1))
      if dotnet restore "$proj" --verbosity quiet \
         && dotnet build "$proj" --no-restore --verbosity quiet -maxcpucount:1 -nodeReuse:false; then
        return 0
      fi
      if [ "$attempt" -ge 2 ]; then
        echo "  ERROR: $proj failed after $attempt attempt(s)" >&2
        return 1
      fi
      echo "  (attempt $attempt failed, retrying after 5s...)"
      sleep 5
    done
  }

  build_service apps/services/liens/Liens.Api/Liens.Api.csproj
  build_service apps/services/identity/Identity.Api/Identity.Api.csproj
  build_service apps/services/documents/Documents.Api/Documents.Api.csproj
  build_service apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj
  build_service apps/services/notifications/Notifications.Api/Notifications.Api.csproj
  build_service apps/gateway/Gateway.Api/Gateway.Api.csproj
else
  echo "SDK < 10 — skipping service project builds (require net10.0). Shared libraries verified OK."
fi

echo "=== Post-merge setup complete ==="
