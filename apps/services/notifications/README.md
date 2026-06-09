# Notifications Service

Multi-channel notification delivery — email (SendGrid/SMTP), SMS (Twilio), push, and webhook.

**Port:** 5025

## Responsibilities

- Transactional and event-driven notification delivery
- Template management (platform defaults + tenant overrides)
- Multi-channel dispatch (email, SMS, push, webhook)
- Governance rules — per-tenant policy packs controlling what can be sent
- Governance approval workflow and release management
- Canary rollout and tenant-segmented governance deployment
- Per-tenant rule scoping and isolation
- Cross-channel governance federation (unified runtime enforcement)
- Notification history and retry management
- Dead-letter queue for blocked/failed notifications

## Layer Structure

```
Notifications.Api/            Endpoints, middleware, Program.cs (port 5025)
Notifications.Application/    Template resolution, delivery orchestration, governance engines
Notifications.Domain/         Notification, Template, GovernanceRule, DeliveryAttempt
Notifications.Infrastructure/ DbContext (NotificationsDb), SendGrid adapter, Twilio adapter
```

## Key Endpoint Groups

| Prefix | Description |
|---|---|
| `/v1/notifications` | Send + list notifications |
| `/v1/templates` | Template CRUD |
| `/notifications/v1/admin/governance/rules` | Governance rule management |
| `/notifications/v1/admin/governance/runtime/` | Runtime status, telemetry, simulate |

## Governance Runtime

Five channel engines: Email, Push, Webhook, SMS (compatibility), and a federation layer. All evaluate against per-tenant governance rules before delivery. Fail-open when `FailOpenOnRuntimeError = true` (default). Decisions are persisted to telemetry for audit.

## External Providers

- **Email:** SendGrid (primary) + SMTP/MailKit (fallback) — configured via `SENDGRID_API_KEY`, `SENDGRID_FROM_EMAIL`, `SENDGRID_FROM_NAME` secrets
- **SMS:** Twilio — configured via `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_FROM_NUMBER` secrets
- **Webhook:** Configurable per-template

## Database

`NotificationsDb` (MySQL, separate from all other services).

## Service Auth

Inbound service calls authenticated via service JWT (`FLOW_SERVICE_TOKEN_SECRET`). Legacy `X-Tenant-Id` header path maintained for backward compatibility.
