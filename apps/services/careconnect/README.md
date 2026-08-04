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
| `DELETE` | `/api/networks/{networkId}/providers/{id}` | Bearer | Soft-delete a provider-location network membership |
| `POST` | `/api/networks/{networkId}/providers/import` | Development-only | CSV/XLSX provider migration/import into a tenant network |

### Provider specialties

CareConnect has a global Specialty catalog that is separate from legacy provider categories. Categories remain in the
API for compatibility, but new provider setup and provider search behavior should use specialties.

- Default active specialties are seeded for Pain, Spine, Physical Therapy, Neuro, Imaging, Chiropractor, and Extremities.
- Providers must have at least one active specialty when they are created or edited through the provider APIs or the tenant network provider setup flow.
- Provider setup accepts an optional professional title (for example, `Dr.`) alongside first and last name; the single `Name` field remains a computed display string for existing consumers.
- Public provider enrollment prefills and submits that same optional title to Identity self-registration, where it is stored on `idt_Users.Title`.
- Multiple specialties are supported. The first selected specialty is treated as the primary specialty for list/detail display.
- Existing provider specialties are backfilled from provider categories when the category code/name maps to one of the seeded specialty values.
- Public network detail responses include active specialty options plus each provider's assigned specialties so public pages do not need a separate anonymous specialty lookup.

Platform administrators can configure the global catalog with `POST /api/specialties`, `PUT /api/specialties/{id}`,
and `DELETE /api/specialties/{id}`. `GET /api/specialties` returns active options by default and supports
`includeInactive=true` for administrative views.

### Provider locations

CareConnect treats `Provider` as the shared identity/profile record and `Facility` as the canonical location record.
NPI identifies the provider identity and remains unique when present. A provider with multiple locations should have
one provider row, one facility row per address, and one `ProviderFacility` link per provider-location pair.

Network membership is location-scoped through `cc_NetworkProviders.FacilityId`. The tenant network, public network,
and referral flows return one row/card/marker per provider-location membership and expose `networkProviderId`,
`providerId`, and `facilityId`. Frontend selection and public/authenticated referral submission should use
`networkProviderId`; the backend validates that the selected membership belongs to the tenant network and stores
`FacilityId` on the referral.

Shared registry search for tenant network setup also returns one result per provider facility so administrators can
add an existing location directly when a matched provider has multiple addresses.

Tenant network provider setup rejects duplicate provider creation by NPI or tenant email. Administrators should
search the shared registry first; if the provider already exists, the supported path for another address is the
explicit Add new location flow, which creates or reuses a `Facility` and adds a provider-location network membership
without creating another `Provider` row.

Tenant network provider editing is provider-scoped but location-aware: opening Edit from any provider-location row
shows all locations for that provider in the selected network. Provider title/name/organization and specialties remain
shared setup fields, while each facility row has its own facility name, contact, address, active flag, and accepting
referrals flag. Deleting a location is a membership soft delete: `DELETE /api/networks/{networkId}/providers/{id}`
marks that `cc_NetworkProviders` row inactive and not accepting referrals instead of removing the row. My Network keeps
inactive locations visible for edit/restore; public network payloads and provider counts include only active
provider-location memberships whose provider and facility are also active.

### Provider import

The development-only provider import endpoint accepts CSV or XLSX uploads at `POST /api/networks/{networkId}/providers/import`.
Each valid row creates or reuses a provider identity, creates or reuses a facility location, links the provider to
that facility, and links that provider-location pair to the network. Matching uses exact NPI first. Blank NPI rows
fall back to tenant email plus provider/facility context. Same NPI plus a different address creates another facility
and another network membership, not another provider.

The import accepts canonical headers and workbook-style headers. Required usable location fields are `email`, `phone`,
address, city, state, and ZIP. `tenantId` is optional when the file is imported through a specific
network; missing row tenant IDs default to the target network tenant, while supplied mismatched tenant IDs are rejected.
`Medical Provider` maps to provider title/name parsing. If
`Medical Provider` is blank, `Medical Facility` becomes the organization-level provider identity. `Medical Facility`
maps to `Facility.Name` and provider organization name. Address columns map to `Facility`; `Address 2` is appended to
`Address 1` during parsing because CareConnect currently has one facility street-address field. `NPI` maps only to
`Provider.Npi`.

Specialty values may be codes or names such as `Pain`, `Spine`, `Physical Therapy`, `Neuro`, `Imaging`,
`Chiropractor`, and `Extremities`; `Chiro` is normalized to `Chiropractor`. Category/provider-type columns are still
accepted for compatibility and are used as a specialty fallback when no specialty column is supplied.

Optional import columns include `title`, `categoryCodes`, `primaryCategoryCode`, `primarySpecialtyCode`, `latitude`,
`longitude`, and `geoPointSource`. `geoPointSource` is normalized to the supported values `Manual`, `Geocoded`, or
`Imported`; common geocoder labels such as `nominatim` are treated as `Geocoded`, and coordinate rows with no source
default to `Imported`. The current sample is `artifacts/postman/careconnect-provider-import.sample.csv`.

### Provider search and distance

Authenticated provider search accepts `specialtyCode` plus ZIP-backed geospatial filters:

- `specialtyCode` filters providers by assigned specialty code.
- `lat`, `lng`, and `radius` filter provider locations by `Facility.Latitude`/`Facility.Longitude` when available, with provider coordinates kept as a compatibility fallback. The repository narrows by bounding box, calculates exact Haversine distance in miles, filters by the requested radius, and sorts matching results by distance.
- Tenant portal ZIP controls geocode ZIP/address input through `/api/geocode/address?loose=1`, then send the derived `lat`, `lng`, and `radius` query params to provider search.

Selected-network public/common pages (`/careconnect/browse-networks/{id}` and `/careconnect/network`) filter the
already-selected network client-side by ZIP and specialty. ZIP search geocodes the entered ZIP/address, displays a
search-location map pin, filters provider-location rows without usable coordinates when a search center is active,
calculates and displays miles from the search point, and sorts provider cards and map markers nearest to farthest.
Users can clear or change ZIP and specialty filters without reloading the page.

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
