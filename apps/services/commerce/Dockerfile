# syntax=docker/dockerfile:1.6

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + central package config first for better layer caching
COPY Commerce.sln Directory.Build.props Directory.Packages.props global.json ./
COPY src/Commerce.Api/Commerce.Api.csproj src/Commerce.Api/
COPY src/Commerce.Application/Commerce.Application.csproj src/Commerce.Application/
COPY src/Commerce.Contracts/Commerce.Contracts.csproj src/Commerce.Contracts/
COPY src/Commerce.Domain/Commerce.Domain.csproj src/Commerce.Domain/
COPY src/Commerce.Infrastructure/Commerce.Infrastructure.csproj src/Commerce.Infrastructure/
COPY tests/Commerce.Tests/Commerce.Tests.csproj tests/Commerce.Tests/

RUN dotnet restore Commerce.sln

# Copy the rest and publish
COPY . .
RUN dotnet publish src/Commerce.Api/Commerce.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Run as non-root
RUN useradd -m -u 10001 commerce
USER commerce

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=1

EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Commerce.Api.dll"]
