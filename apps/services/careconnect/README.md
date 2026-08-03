# CareConnect Service

Healthcare provider directory, referral management, and appointment scheduling.

**Port:** 5003

## Responsibilities

- Provider network management (create, activate, search, geo-discovery)
- Global provider specialty catalog and provider-to-specialty assignment
- Referral lifecycle (Draft → Submitted → Accepted → Completed)
- Appointment scheduling against provider availability slots
- Attachment management for referrals and appointments
- Referral and appointment notes
- Notification delivery on key lifecycle events

## Layer Structure

```
CareConnect.Api/           Endpoints, middleware, Program.cs (port 5003)
CareConnect.Application/   Interfaces, DTOs, services
CareConnect.Domain/        Provider, Specialty, Referral, Appointment, Availability, Attachment
CareConnect.Infrastructure/ DbContext, repositories, EF migrations
CareConnect.Tests/         Tests
```

## Key Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/careconnect/providers` | Bearer | Search providers |
| `GET` | `/api/careconnect/providers/{id}` | Bearer | Provider detail |
| `GET` | `/api/specialties` | Bearer | List configured CareConnect specialties |
| `POST` | `/api/specialties` | PlatformAdmin | Create a global specialty |
| `PUT` | `/api/specialties/{id}` | PlatformAdmin | Update a global specialty |
| `DELETE` | `/api/specialties/{id}` | PlatformAdmin | Deactivate a global specialty |
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
| `PUT` | `/api/networks/{networkId}/providers/{providerId}` | Bearer | Edit a provider from a tenant network after membership validation |

### Provider specialties

CareConnect has a global Specialty catalog that is separate from legacy provider categories. Categories remain in the
API for compatibility, but new provider setup and provider search behavior should use specialties.

- Default active specialties are seeded for Pain, Spine, Physical Therapy, Neuro, Imaging, and Chiropractor.
- Providers must have at least one active specialty when they are created or edited through the provider APIs or the tenant network provider setup flow.
- Provider setup accepts an optional professional title (for example, `Dr.`) alongside first and last name; the single `Name` field remains a computed display string for existing consumers.
- Public provider enrollment prefills and submits that same optional title to Identity self-registration, where it is stored on `idt_Users.Title`.
- Multiple specialties are supported. The first selected specialty is treated as the primary specialty for list/detail display.
- Existing provider specialties are backfilled from provider categories when the category code/name maps to one of the seeded specialty values.
- Public network detail responses include active specialty options plus each provider's assigned specialties so public pages do not need a separate anonymous specialty lookup.

Platform administrators can configure the global catalog with `POST /api/specialties`, `PUT /api/specialties/{id}`,
and `DELETE /api/specialties/{id}`. `GET /api/specialties` returns active options by default and supports
`includeInactive=true` for administrative views.

### Provider search and distance

Authenticated provider search accepts `specialtyCode` plus ZIP-backed geospatial filters:

- `specialtyCode` filters providers by assigned specialty code.
- `lat`, `lng`, and `radius` filter providers by location. The repository narrows by bounding box, calculates exact Haversine distance in miles, filters by the requested radius, and sorts matching results by distance.
- Tenant portal ZIP controls geocode ZIP/address input through `/api/geocode/address?loose=1`, then send the derived `lat`, `lng`, and `radius` query params to provider search.

Selected-network public/common pages (`/careconnect/browse-networks/{id}` and `/careconnect/network`) filter the
already-selected network client-side by ZIP and specialty. ZIP search geocodes the entered ZIP/address, displays a
search-location map pin, filters providers without usable coordinates when a search center is active, calculates and
displays miles from the search point, and sorts provider cards and map markers by distance. Users can clear or change
ZIP and specialty filters without reloading the page.

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
