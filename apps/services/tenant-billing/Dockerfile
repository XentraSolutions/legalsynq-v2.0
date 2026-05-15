# Multi-stage build for the Billing microservice (Billing.Api).
#
# This image is intended to run as a private internal service, never
# exposed directly to browsers. Reach it only via the Monk BFF, which
# supplies the BILLING_INTERNAL_TOKEN and X-Tenant-Id at request time.
#
# Required environment at runtime:
#   BILLING_INTERNAL_TOKEN          shared secret enforced on /api/*
#   BILLING_DB_CONNECTION           MySQL connection string (or
#                                   ConnectionStrings__Billing equivalent)
#
# Optional environment:
#   BILLING_RUN_MIGRATIONS=true     run EF migrations on startup
#                                   (auto-on when ASPNETCORE_ENVIRONMENT=Development)
#   BILLING_ENABLE_PLATFORM_TEMPLATES=true
#                                   enables /api/invoice-templates/platform/*
#                                   (default: disabled, returns 404)

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy MSBuild orchestration first so restore can be cached.
COPY Billing.sln                ./
COPY Directory.Build.props      ./
COPY Directory.Packages.props   ./
COPY global.json                ./
COPY .config/                   ./.config/

# Copy project files for restore.
COPY src/Billing.Api/Billing.Api.csproj                       src/Billing.Api/
COPY src/Billing.Domain/Billing.Domain.csproj                 src/Billing.Domain/
COPY src/Billing.Infrastructure/Billing.Infrastructure.csproj src/Billing.Infrastructure/
COPY tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj   tests/Billing.Domain.Tests/
COPY tests/Billing.Tests/Billing.Tests.csproj                 tests/Billing.Tests/

RUN dotnet restore Billing.sln

# Copy the rest of the source and publish only the API.
COPY src/  src/
COPY tests/ tests/
RUN dotnet publish src/Billing.Api/Billing.Api.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default to the chosen private port (5007). MS-BILL-SVC-003 picked this
# because 5005 collides with `ncm` and 5006 is also taken in the existing
# Monk Search port plan. Override with ASPNETCORE_URLS at deploy time if
# the fleet allocates a different port.
EXPOSE 5007
ENV ASPNETCORE_URLS=http://+:5007
ENV ASPNETCORE_ENVIRONMENT=Production

# Compose / runtime registration (e.g. wiring this into the Monk Search
# top-level docker-compose.yml) is intentionally deferred to a later
# prompt — Billing must remain internal-only until MS-BILL-BFF-005.

ENTRYPOINT ["dotnet", "Billing.Api.dll"]
