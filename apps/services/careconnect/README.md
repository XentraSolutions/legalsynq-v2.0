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
| `GET` | `/api/careconnect/referrals` | Bearer | List referrals with queue and participant filters |
| `GET` | `/api/assistant-tools/referrals/search` | Bearer | Assistant-only referral search surface |
| `GET` | `/api/assistant-tools/referrals/queue-summary` | Bearer | Assistant-only referral queue and KPI summary |
| `GET` | `/api/assistant-tools/referrals/{id}` | Bearer | Assistant referral lookup with recent history |
| `GET` | `/api/assistant-tools/referrals/{id}/history` | Bearer | Assistant referral history lookup |
| `GET` | `/api/assistant-tools/providers/search` | Bearer | Assistant-only provider lookup |
| `GET` | `/api/assistant-tools/referrers/search` | Bearer | Assistant-only referrer lookup |
| `GET` | `/api/careconnect/appointments` | Bearer | List appointments |
| `POST` | `/api/careconnect/appointments` | Bearer | Book appointment |
| `GET` | `/api/public/careconnect/network` | Anonymous | Public provider network |

### Referral list and lookup filters

`GET /api/careconnect/referrals` supports the standard paging inputs plus assistant-friendly filtering fields:

- `search` tokenizes natural-language phrases and matches across patient/client name, referrer contact, law-firm name, provider name, and provider organization name
- `providerName` to narrow results to a specific receiving provider or provider organization
- `referrerName` to narrow results to a referrer contact, law firm, or referring organization
- Existing queue filters such as `status`, `createdFrom`, `createdTo`, `providerId`, `referrerUserId`, and page params

These read-only filters are used by the tenant portal and by CareConnect's dedicated assistant-tool API. Xenia now
calls the assistant-only endpoints under `/api/assistant-tools/*` instead of composing results from the end-user
referral and provider APIs itself.

`GET /api/assistant-tools/referrals/queue-summary` also accepts assistant KPI filters for count-style questions:

- `status` for a single canonical referral status
- `statusGroup` for assistant-friendly groups: `new`, `open`, or `closed`
- `days` for relative windows such as "last 7 days"
- `createdFrom` and `createdTo` for explicit date ranges

The response includes total visible referrals, counts within the requested window, matching count after any
status/status-group filter, status breakdowns, and recent matching referrals for grounding.

### Assistant tool API

CareConnect owns its grounded assistant contract for referral and provider workflows. The assistant-only endpoints:

- Reuse the caller's normal bearer-token access and participant scoping
- Return tool-shaped JSON for referral lookup/history, referral search, provider search, referrer search, and queue/KPI summaries
- Keep product-specific lookup composition inside CareConnect instead of in Xenia

### Referral message attachments

Referral comment endpoints accept both the existing JSON body for text-only comments and `multipart/form-data` when
message-scoped attachments are included:

- Public token flow: `POST /api/public/referrals/thread/comments?token=...` with `senderType`, `message`, and repeated `files`
- Authenticated flow: `POST /api/referrals/{referralId}/comments` with `message` and repeated `files`

A comment must include message text, at least one attachment, or both. Message text is limited to 4000 characters.
Each message can include up to 10 files. File size and MIME validation reuse the service's existing attachment upload
settings, currently 50 MB per file with the configured PDF, image, Office document, text, and CSV allowlist.

Files are uploaded to the Documents service with `referenceType = "referral-comment"`. CareConnect stores only
attachment metadata in `cc_ReferralAttachments`, linked to the creating comment by `ReferralCommentId`. Thread reads
return these attachments on each comment, but the general referral documents list excludes message-scoped attachments.
Clients should open files only through signed URL endpoints:

- Authenticated: `/api/referrals/{referralId}/attachments/{attachmentId}/url`
- Public token: `/api/referrals/{referralId}/public-attachments/{attachmentId}/url?token=...`

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
