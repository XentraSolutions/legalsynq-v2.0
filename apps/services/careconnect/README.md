# CareConnect Service

Healthcare provider directory, referral management, and appointment scheduling.

**Port:** 5003

## Responsibilities

- Provider network management (create, activate, search, geo-discovery)
- Referral lifecycle (Draft → Submitted → Accepted → Completed)
- Appointment scheduling against provider availability slots
- Attachment management for referrals and appointments
- Referral and appointment notes
- Notification delivery on key lifecycle events

## Layer Structure

```
CareConnect.Api/           Endpoints, middleware, Program.cs (port 5003)
CareConnect.Application/   Interfaces, DTOs, services
CareConnect.Domain/        Provider, Referral, Appointment, Availability, Attachment
CareConnect.Infrastructure/ DbContext, repositories, EF migrations
CareConnect.Tests/         Tests
```

## Key Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/careconnect/providers` | Bearer | Search providers |
| `GET` | `/api/careconnect/providers/{id}` | Bearer | Provider detail |
| `POST` | `/api/careconnect/referrals` | Bearer | Create referral |
| `GET` | `/api/careconnect/referrals` | Bearer | List referrals |
| `GET` | `/api/careconnect/appointments` | Bearer | List appointments |
| `POST` | `/api/careconnect/appointments` | Bearer | Book appointment |
| `GET` | `/api/public/careconnect/network` | Anonymous | Public provider network |

## Product Roles

| Role | Access |
|---|---|
| `CARECONNECT_REFERRER` | Send referrals, find providers, book appointments |
| `CARECONNECT_RECEIVER` | Receive referrals, manage appointments, manage availability |

## Database

`CareConnectDb` (MySQL).

## External Integrations

- **Identity service** — provider provisioning via `CareConnectProvisioningHandler` (registered in Identity's product provisioning pipeline)
- **Audit service** — all key events published
- **Notifications service** — referral and appointment event notifications
