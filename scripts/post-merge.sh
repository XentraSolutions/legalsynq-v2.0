#!/bin/bash
set -e

echo "=== Post-merge setup ==="

echo "Installing frontend dependencies..."
pnpm install --frozen-lockfile 2>/dev/null || pnpm install

echo "Building .NET services (restore + build)..."
# Build one project at a time and apply aggressive memory limits to avoid OOM
# crashes in the constrained Replit build environment.
export DOTNET_GCConserveMemory=9
export DOTNET_CLI_TELEMETRY_OPTOUT=1
# Disable MSBuild node reuse so each build releases memory fully before the next
export MSBUILDDISABLENODEREUSE=1
# Cap the GC heap to ~400 MB per build process
export DOTNET_GCHeapHardLimit=419430400

build_project() {
  local proj="$1"
  echo "  -> building $proj"
  dotnet restore "$proj" --verbosity quiet
  dotnet build   "$proj" --no-restore --verbosity quiet -maxcpucount:1 -nodeReuse:false
}

build_project apps/services/liens/Liens.Api/Liens.Api.csproj
build_project apps/services/identity/Identity.Api/Identity.Api.csproj
build_project apps/services/documents/Documents.Api/Documents.Api.csproj
build_project apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj
build_project apps/services/notifications/Notifications.Api/Notifications.Api.csproj
build_project apps/gateway/Gateway.Api/Gateway.Api.csproj

echo "=== Post-merge setup complete ==="
