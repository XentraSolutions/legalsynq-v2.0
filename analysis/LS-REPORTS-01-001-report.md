# LS-REPORTS-01-001 — Template Data Model & Persistence Foundation

## Status: IN PROGRESS

## Objective
Introduce MySQL + EF Core persistence foundation and implement the core data model for report templates and versioning.

## Steps

### Step A: Add Dependencies
- **Status**: PENDING
- **Files Modified**: `Reports.Infrastructure.csproj`, `Reports.Api.csproj`
- **Packages**: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Pomelo.EntityFrameworkCore.MySql`

### Step B: Configuration
- **Status**: PENDING
- **Files Created/Modified**: `appsettings.json`, `ReportsServiceSettings.cs`
- **Notes**: Connection string via `ConnectionStrings:ReportsDb`, consistent with platform pattern

### Step C: Domain Entities
- **Status**: PENDING
- **Files Modified**: `ReportDefinition.cs`, `ReportExecution.cs`
- **Files Created**: `ReportTemplateVersion.cs`
- **Notes**: Domain layer stays clean — no EF dependencies

### Step D: EF Core DbContext
- **Status**: PENDING
- **Files Created**: `ReportsDbContext.cs`
- **Notes**: Infrastructure layer, Pomelo MySQL provider

### Step E: Entity Configurations
- **Status**: PENDING
- **Files Created**: Entity configuration classes
- **Notes**: Fluent API, table prefix `rpt_`

### Step F: Persistence Contracts
- **Status**: PENDING
- **Files Modified**: `IReportRepository.cs`
- **Files Created**: `ITemplateRepository.cs`
- **Notes**: Typed contracts replacing bootstrap `object` signatures

### Step G: EF Repository Implementations
- **Status**: PENDING
- **Files Created**: `EfReportRepository.cs`, `EfTemplateRepository.cs`
- **Notes**: Replace mock implementations

### Step H: DI Registration & Program.cs
- **Status**: PENDING
- **Files Modified**: `DependencyInjection.cs`, `Program.cs`
- **Notes**: Register DbContext, swap mock repos for EF implementations

### Step I: Health Probe Update
- **Status**: PENDING
- **Files Modified**: `HealthEndpoints.cs`
- **Notes**: Add database connectivity check to readiness probe

### Step J: Build Validation
- **Status**: PENDING
- **Notes**: Full solution build, 0 errors / 0 warnings

---

## Issues Encountered
_(none yet)_

## Decisions Made
_(none yet)_
