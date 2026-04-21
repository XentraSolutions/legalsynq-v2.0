#!/bin/bash
set -e

echo "=== Post-merge setup ==="

echo "Installing frontend dependencies..."
pnpm install --frozen-lockfile 2>/dev/null || pnpm install

echo "Building .NET services (restore + build)..."
# Build one project at a time (-maxcpucount:1) and cap GC memory use to avoid
# OOM crashes in the constrained Replit build environment.
export DOTNET_GCConserveMemory=9
export DOTNET_CLI_TELEMETRY_OPTOUT=1

dotnet restore apps/services/liens/Liens.Api/Liens.Api.csproj --verbosity quiet
dotnet build apps/services/liens/Liens.Api/Liens.Api.csproj --no-restore --verbosity quiet -maxcpucount:1

dotnet restore apps/services/identity/Identity.Api/Identity.Api.csproj --verbosity quiet
dotnet build apps/services/identity/Identity.Api/Identity.Api.csproj --no-restore --verbosity quiet -maxcpucount:1

dotnet restore apps/services/fund/Fund.Api/Fund.Api.csproj --verbosity quiet
dotnet build apps/services/fund/Fund.Api/Fund.Api.csproj --no-restore --verbosity quiet -maxcpucount:1

dotnet restore apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj --verbosity quiet
dotnet build apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj --no-restore --verbosity quiet -maxcpucount:1

dotnet restore apps/gateway/Gateway.Api/Gateway.Api.csproj --verbosity quiet
dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --verbosity quiet -maxcpucount:1

echo "=== Post-merge setup complete ==="
