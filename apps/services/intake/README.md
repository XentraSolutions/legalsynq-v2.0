# Synq Intake Service

Synq Intake is the product-neutral LegalSynq v3 service boundary for future
tenant-owned intake channels. LSI-B01-01 establishes the independently
startable technical foundation only; it does not implement mailbox processing
or intake business workflows.

## Architecture

- **Tenant-aware:** authenticated requests use the existing LegalSynq request
  context. Arbitrary tenant IDs are not accepted as a request override.
- **Product-neutral:** the service is not owned by SynqLien and does not contain
  lien-specific business logic.
- **Independently persisted:** Intake owns `IntakeDbContext` and the
  `IntakeDatabase` connection-string boundary. It does not reuse Liens,
  Documents, Identity, or any other service database.
- **Independently startable:** the service has its own process, API, layered
  projects, health endpoints, and development port.

Each tenant will designate its own Synq Intake inbound email address.
Tenant identity for future inbound email processing will be resolved from
registered tenant-owned Intake source configuration, not inferred from message
contents, sender data, attachments, document metadata, classification, or AI.
Each configured address will belong to exactly one tenant. The foundation does
not assume one shared mailbox and remains compatible with multiple Intake
sources per tenant.

## Technology stack

- .NET 10 / ASP.NET Core Minimal APIs
- C#
- Entity Framework Core with Pomelo.EntityFrameworkCore.MySql
- Existing LegalSynq JWT bearer and service-token conventions
- Existing BuildingBlocks request context
- Serilog structured console logging
- xUnit tests

## Project structure

```text
apps/services/intake/
├── Intake.Api/
├── Intake.Application/
├── Intake.Domain/
├── Intake.Infrastructure/
├── Intake.Contracts/
├── Intake.Tests/
└── README.md
```

## Local startup

```bash
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5013 \
dotnet run --project apps/services/intake/Intake.Api/Intake.Api.csproj
```

The development startup scripts also build and start Intake on port `5013`.
The gateway exposes it additively at `/api/intake/*`.

## Configuration

| Setting | Purpose |
|---|---|
| `ConnectionStrings__IntakeDatabase` | Dedicated MySQL 8 connection string |
| `Jwt__SigningKey` | Existing user JWT signing key |
| `Jwt__Issuer` | Existing JWT issuer |
| `Jwt__Audience` | Existing JWT audience |
| `ServiceTokens__SigningKey` / `FLOW_SERVICE_TOKEN_SECRET` | Existing service-token validation secret |

The foundation can start without `IntakeDatabase` in Development so liveness
can be inspected. Readiness reports the database as unavailable until a valid
MySQL connection is configured.

## Endpoints

- `GET /health` — process liveness; anonymous
- `GET /health/ready` — readiness including the dedicated database check; anonymous
- `GET /info` — non-sensitive service metadata; anonymous
- `GET /swagger` — development-only OpenAPI UI
- `GET /tenant-context` — authenticated request-context diagnostic foundation endpoint

## Database ownership and migrations

`IntakeDbContext` is intentionally empty in this ticket. No placeholder
business tables are created. Migration work is deferred to a later LSI-B01
iteration when a meaningful Intake model exists. No migrations are applied to
AWS or shared environments by this repository phase.

## Explicit current exclusions

This foundation does **not** implement:

- `TenantIntakeSource` CRUD or mailbox connection UI
- Microsoft 365, Google, IMAP, email polling, or email synchronization
- raw EML or structured email persistence
- attachment ingestion or Documents integration
- Synq AI, processing profiles, classification, extraction, or normalization
- matching, duplicate detection, confidence scoring, or review
- Case, Facility, Lien, Flow, HL7, SFTP, or external intake API integration
- straight-through automation or product-specific UI