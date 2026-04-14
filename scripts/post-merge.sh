#!/bin/bash
set -e

echo "=== Post-merge setup ==="

echo "Installing frontend dependencies..."
pnpm install --frozen-lockfile 2>/dev/null || pnpm install

echo "Building .NET services (restore + build)..."
dotnet restore apps/services/liens/Liens.Api/Liens.Api.csproj --verbosity quiet
dotnet build apps/services/liens/Liens.Api/Liens.Api.csproj --no-restore --verbosity quiet

dotnet restore apps/services/identity/Identity.Api/Identity.Api.csproj --verbosity quiet
dotnet build apps/services/identity/Identity.Api/Identity.Api.csproj --no-restore --verbosity quiet

dotnet restore apps/services/fund/Fund.Api/Fund.Api.csproj --verbosity quiet
dotnet build apps/services/fund/Fund.Api/Fund.Api.csproj --no-restore --verbosity quiet

dotnet restore apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj --verbosity quiet
dotnet build apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj --no-restore --verbosity quiet

dotnet restore apps/gateway/Gateway.Api/Gateway.Api.csproj --verbosity quiet
dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --verbosity quiet

echo "=== Post-merge setup complete ==="
