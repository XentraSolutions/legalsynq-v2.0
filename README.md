# LegalSynq Platform

Multi-tenant SaaS platform for legal, healthcare, and funding operations. Built on .NET 8 microservices behind a YARP gateway with two Next.js 15 frontends.

## Products

| Code | Name | Description |
|---|---|---|
| `SYNQLIEN` | SynqLien | Medical lien lifecycle — creation, marketplace, offers, purchase, servicing |
| `SYNQFUND` | SynqFund | Funding application workflow — submission, review, approval |
| `CARECONNECT` | CareConnect | Healthcare referral network and appointment scheduling |

## Architecture

```
Tenant App (port 5000)     Control Center (port 5004)
        │                           │
        └───────────────────────────┘
                        │
                  Gateway / YARP (port 5010)
                        │
   ┌────────────────────┼──────────────────────┐
Identity  Tenant  CareConnect  Liens  Fund  Documents
 :5001    :5005     :5003      :5002  :5008   :5006
   │
Notifications  Audit  Monitoring  Reports  Flow  Task
   :5025        :5007    :5020      :5029  :5015 :5016
```

- Gateway validates JWTs and routes to downstream services. Each service also validates JWTs independently.
- Frontend uses BFF pattern — server route handlers proxy to the gateway. Clients never handle raw JWTs.
- All services share no databases or EF contexts.
- Shared libraries (`shared/`) are additive — services remain independently startable.

## Quick Start

```bash
bash scripts/run-dev.sh
```

The dev proxy at port 5000 gates the browser until Next.js is warm (allow ~30 seconds cold start).

## Repository Layout

```
apps/
  gateway/             YARP reverse proxy
  web/                 Tenant portal (Next.js 15)
  control-center/      Operator admin portal (Next.js 15)
  services/
    identity/          Auth, users, orgs, products, RBAC
    tenant/            Canonical tenant registry and branding
    careconnect/       Referrals, providers, appointments
    liens/             SynqLien marketplace and servicing
    fund/              SynqFund funding applications
    documents/         Document storage, scanning, access tokens
    notifications/     Multi-channel notification delivery
    audit/             Tamper-evident audit event log
    monitoring/        Service health probes and alerting
    reports/           Report templates, execution, export, scheduling
    flow/              Workflow engine and task management
    task/              Platform task service
    support/           Support case management
    commerce/          Commerce/billing integration (in progress)
    tenant-billing/    Tenant-side billing
shared/
  contracts/           Shared C# DTOs, event types, notification keys
  building-blocks/     Middleware, auth helpers, Commerce abstractions
  audit-client/        Typed audit event client (LegalSynq.AuditClient)
scripts/
  run-dev.sh           Starts all services in parallel
analysis/              Per-ticket implementation reports
```

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 15.2.9, React 18, TypeScript, Tailwind CSS |
| Gateway | ASP.NET Core + YARP |
| Services | ASP.NET Core 8 Minimal APIs |
| ORM | Entity Framework Core 8 (Pomelo MySQL) |
| Database | MySQL 8 (AWS RDS) |
| Auth | JWT Bearer tokens + HttpOnly session cookies |
| Email | SendGrid, SMTP (MailKit) |
| SMS | Twilio |

## Service READMEs

- [Gateway](apps/gateway/README.md)
- [Tenant Portal](apps/web/README.md)
- [Control Center](apps/control-center/README.md)
- [Identity Service](apps/services/identity/README.md)
- [Tenant Service](apps/services/tenant/README.md)
- [CareConnect Service](apps/services/careconnect/README.md)
- [Liens Service](apps/services/liens/README.md)
- [Fund Service](apps/services/fund/README.md)
- [Documents Service](apps/services/documents/README.md)
- [Notifications Service](apps/services/notifications/README.md)
- [Audit Service](apps/services/audit/README.md)
- [Monitoring Service](apps/services/monitoring/README.md)
- [Reports Service](apps/services/reports/README.md)
- [Flow Service](apps/services/flow/README.md)
- [Shared Libraries](shared/README.md)
