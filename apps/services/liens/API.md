# Liens Service API Documentation

## Table of Contents

- [Overview](#overview)
- [Authentication & Authorization](#authentication--authorization)
- [Permissions Reference](#permissions-reference)
- [Common Models](#common-models)
- [Error Responses](#error-responses)
- [Liens](#liens-endpoints)
- [Cases](#cases-endpoints)
- [Bills of Sale](#bills-of-sale-endpoints)
- [Lien Offers](#lien-offers-endpoints)
- [Contacts](#contacts-endpoints)
- [Servicing](#servicing-endpoints)
- [Reports](#reports-endpoints)

---

## Overview

Base URL prefix: `/api/liens`

All endpoints in the Liens service are JSON-based (except document download endpoints which return files). Request and response bodies use `application/json` content type unless otherwise noted.

---

## Authentication & Authorization

Every endpoint requires:

1. **Authenticated user** — the caller must be an authenticated user (policy: `AuthenticatedUser`).
2. **Product access** — the caller must have access to the `SYNQ_LIENS` product.
3. **Endpoint-specific permission** — each endpoint requires a specific permission as listed in the tables below.

Requests missing authentication receive a `401 Unauthorized` response.

---

## Permissions Reference

| Permission Code | Description |
|---|---|
| `SYNQ_LIENS.lien:read` | Read liens, bills of sale, and lien offers |
| `SYNQ_LIENS.lien:create` | Create new liens |
| `SYNQ_LIENS.lien:update` | Update existing liens; accept lien offers |
| `SYNQ_LIENS.lien:offer` | Create lien offers |
| `SYNQ_LIENS.lien:service` | Manage bills of sale lifecycle (submit/execute/cancel); manage contacts and servicing items |
| `SYNQ_LIENS.case:read` | Read cases |
| `SYNQ_LIENS.case:create` | Create new cases |
| `SYNQ_LIENS.case:update` | Update existing cases |

The following permissions are defined in the system but not currently used by any API endpoint:

| Permission Code | Description |
|---|---|
| `SYNQ_LIENS.lien:read:own` | Read own liens |
| `SYNQ_LIENS.lien:browse` | Browse liens |
| `SYNQ_LIENS.lien:purchase` | Purchase liens |
| `SYNQ_LIENS.lien:read:held` | Read held liens |
| `SYNQ_LIENS.lien:settle` | Settle liens |

---

## Common Models

### PaginatedResult\<T\>

All list/search endpoints return results wrapped in this paginated envelope.

| Field | Type | Description |
|---|---|---|
| `items` | `T[]` | Array of result items for the current page |
| `page` | `integer` | Current page number |
| `pageSize` | `integer` | Number of items per page |
| `totalCount` | `integer` | Total number of matching items across all pages |

---

## Error Responses

### 401 Unauthorized

Returned when the request lacks valid authentication credentials.

### 403 Forbidden

Returned when the user is authenticated but does not have the required product access (`SYNQ_LIENS`) or the endpoint-specific permission.

### 404 Not Found

Returned when a requested resource does not exist.

```json
{
  "error": {
    "code": "not_found",
    "message": "Resource description not found."
  }
}
```

### Common Status Codes

In addition to the endpoint-specific success and error codes documented below, **every** endpoint may return:

| Status | Condition |
|---|---|
| `401 Unauthorized` | Missing or invalid authentication |
| `403 Forbidden` | Authenticated but lacking required product access or permission |

**Per-endpoint status code summary:**

| Endpoint Type | Success | Possible Errors |
|---|---|---|
| List / Search (`GET` returning paginated results) | `200 OK` | `401`, `403` |
| Get by ID / Get by number (`GET` returning single item) | `200 OK` | `401`, `403`, `404` |
| Create (`POST`) | `201 Created` | `401`, `403` |
| Update / Action (`PUT` or `POST` on `{id}`) | `200 OK` | `401`, `403`, `404` |
| Document download (`GET` returning file) | `200 OK` | `401`, `403`, `404` |

---

## Liens Endpoints

Base path: `/api/liens/liens`

### GET `/api/liens/liens`

Search and list liens with optional filters.

Buying-facing lien list responses exclude liens in `Rejected`, `Declined`, or `Cancelled` status and normalize the remaining statuses to `Open` or `Closed`. Selling-specific workflow statuses remain available on selling endpoints and on direct lien detail responses.
All liens API timestamp responses are serialized in U.S. Pacific time (`-07:00` or `-08:00` depending on DST). Legacy string-formatted timestamps use the same Pacific conversion.

**Permission:** `SYNQ_LIENS.lien:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by lien status |
| `lienType` | `string` | No | `null` | Filter by lien type |
| `caseId` | `guid` | No | `null` | Filter by associated case ID |
| `facilityId` | `guid` | No | `null` | Filter by facility ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<LienResponse>
```

---

### GET `/api/liens/liens/{id}`

Get a lien by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Lien unique identifier |

**Response:** `200 OK` — `LienResponse`

**Error:** `404 Not Found` — if the lien does not exist.

---

### GET `/api/liens/liens/by-number/{lienNumber}`

Get a lien by its lien number.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `lienNumber` | `string` | Lien number |

**Response:** `200 OK` — `LienResponse`

**Error:** `404 Not Found` — if the lien does not exist.

---

### POST `/api/liens/liens`

Create a new lien.

**Permission:** `SYNQ_LIENS.lien:create`

**Request Body: `CreateLienRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `lienNumber` | `string` | Yes | No | Unique lien number |
| `externalReference` | `string` | No | Yes | External reference identifier |
| `lienType` | `string` | Yes | No | Type of lien |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `facilityId` | `guid` | No | Yes | Associated facility ID |
| `originalAmount` | `decimal` | Yes | No | Original lien amount |
| `jurisdiction` | `string` | No | Yes | Jurisdiction |
| `isConfidential` | `boolean` | Yes | No | Whether the lien is confidential |
| `subjectFirstName` | `string` | No | Yes | Subject first name |
| `subjectLastName` | `string` | No | Yes | Subject last name |
| `incidentDate` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `description` | `string` | No | Yes | Description |

**Response:** `201 Created` — `LienResponse`

Returns the created lien with a `Location` header pointing to `/api/liens/liens/{id}`.

---

### PUT `/api/liens/liens/{id}`

Update an existing lien.

**Permission:** `SYNQ_LIENS.lien:update`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Lien unique identifier |

**Request Body: `UpdateLienRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `externalReference` | `string` | No | Yes | External reference identifier |
| `lienType` | `string` | Yes | No | Type of lien |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `facilityId` | `guid` | No | Yes | Associated facility ID |
| `originalAmount` | `decimal` | Yes | No | Original lien amount |
| `jurisdiction` | `string` | No | Yes | Jurisdiction |
| `isConfidential` | `boolean` | No | Yes | Whether the lien is confidential |
| `subjectFirstName` | `string` | No | Yes | Subject first name |
| `subjectLastName` | `string` | No | Yes | Subject last name |
| `incidentDate` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `description` | `string` | No | Yes | Description |

**Response:** `200 OK` — `LienResponse`

**Error:** `404 Not Found` — if the lien does not exist.

---

### LienResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `lienNumber` | `string` | No | Lien number |
| `externalReference` | `string` | Yes | External reference |
| `lienType` | `string` | No | Type of lien |
| `status` | `string` | No | Current status. Buying list endpoints exclude `Rejected`, `Declined`, and `Cancelled` liens and normalize remaining values to `Open` or `Closed`; direct lien detail responses may still return workflow statuses used by selling flows. |
| `caseId` | `guid` | Yes | Associated case ID |
| `facilityId` | `guid` | Yes | Associated facility ID |
| `originalAmount` | `decimal` | No | Original lien amount |
| `currentBalance` | `decimal` | Yes | Current balance |
| `offerPrice` | `decimal` | Yes | Current offer price |
| `purchasePrice` | `decimal` | Yes | Purchase price |
| `payoffAmount` | `decimal` | Yes | Payoff amount |
| `jurisdiction` | `string` | Yes | Jurisdiction |
| `isConfidential` | `boolean` | No | Confidentiality flag |
| `subjectFirstName` | `string` | Yes | Subject first name |
| `subjectLastName` | `string` | Yes | Subject last name |
| `subjectDisplayName` | `string` | Yes | Computed subject display name |
| `orgId` | `guid` | No | Owning organization ID |
| `sellingOrgId` | `guid` | Yes | Selling organization ID |
| `buyingOrgId` | `guid` | Yes | Buying organization ID |
| `holdingOrgId` | `guid` | Yes | Holding organization ID |
| `incidentDate` | `date` | Yes | Date of incident |
| `description` | `string` | Yes | Description |
| `openedAtUtc` | `datetime` | Yes | When the lien was opened |
| `closedAtUtc` | `datetime` | Yes | When the lien was closed |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Selling Endpoints

Base path: `/api/liens/selling`

### POST `/api/liens/selling/liens/{lienId}/confirm-sale`

Confirms a prepared seller lien for sale. The endpoint moves a draft/prepared lien to `Offered` with
`SellerStatus=SubmittedForSale`, copies the persisted `AskAmount` into `OfferPrice`, and keeps `SoldAtUtc` null.

**Permission:** `SYNQ_LIENS.lien_sale:update`

**Headers:**

| Header | Required | Description |
|---|---|---|
| `Idempotency-Key` | No | Used with tenant/lien/buyer contact to suppress duplicate buyer email sends on replay |

**Request:**

```json
{
  "confirmationAccepted": true,
  "sendBuyerNotification": true
}
```

When `sendBuyerNotification=true`, the lien must have real `FundingCompanyId`, `FundingCompanyContactId`,
`InitialServiceDate`, `AskAmount`, buyer email, seller name/company/email, and handling law firm data. The API creates a
30-day buyer response access link and a separate 30-day seller read-only access link from
`Liens:Selling:BuyerPortalBaseUrl`; callers do not provide CTA URLs. If the explicit base URL is absent, the API
derives it from `SYNQLIEN_COMMON_PORTAL_HOSTNAME`; `synqlien-demo.localhost` resolves to
`http://synqlien-demo.localhost:5000/selling/public` for the full `scripts/run-dev.sh` proxy. The configured buyer
portal base URL must be absolute and must match the active tenant-web browser origin; use
`http://synqlien-demo.localhost:3000/selling/public` when running only `pnpm --dir apps/web dev`. Literal loopback hosts
such as `localhost` or `127.0.0.1` are rejected because the email CTA must work from the recipient's inbox, while named
`.localhost` aliases such as `synqlien-demo.localhost` are allowed for local demo runs. The buyer email uses the
`New Lien Offer` copy with a response CTA. After the buyer email is submitted, the seller receives the same branded
format with buyer/funding-company information and a read-only `View Lien Details` CTA. Neither email inserts sample
document data; both include only real supporting document names found in lien/case document metadata. The LegalSynq mark
and section icons are sent as inline CID image attachments; no remote placeholder assets are required.
For a CTA hosted by the tenant portal, use
`Liens__Selling__BuyerPortalBaseUrl=http://<portal-host>:<web-port>/selling/public` for local demo runs, or
`https://<portal-host>/selling/public` behind a real portal domain; that public browser route renders in `apps/web`,
fetches the Liens JSON endpoint through the gateway, and does not require a `platform_session` cookie. The confirm-sale email disables SendGrid click tracking for this
CTA so the recipient receives the real LegalSynq portal URL instead of a provider tracking URL.

Local SynqLien demo portal example:

```bash
SYNQLIEN_COMMON_PORTAL_HOSTNAME=synqlien-demo.localhost
PORTAL_SYNQLIEN_SUBDOMAIN=synqlien-demo
Liens__Selling__BuyerPortalBaseUrl=http://synqlien-demo.localhost:5000/selling/public
# or, when only apps/web dev is running:
Liens__Selling__BuyerPortalBaseUrl=http://synqlien-demo.localhost:3000/selling/public
```

**Response:** `200 OK`

```json
{
  "lienId": "guid",
  "lienCode": "LIEN-001",
  "status": "Offered",
  "sellerStatus": "SubmittedForSale",
  "askAmount": 2500.00,
  "offerPrice": 2500.00,
  "submittedForSaleAtUtc": "2026-07-22T00:00:00Z",
  "soldAtUtc": null,
  "notification": {
    "requested": true,
    "submitted": true,
    "notificationId": "guid",
    "notificationStatus": "sent",
    "buyerAccessLinkId": "guid",
    "buyerPortalUrl": "<configured-buyer-portal-url>/<token>",
    "expiresAtUtc": "2026-08-21T00:00:00Z",
    "buyerContactId": "guid",
    "buyerOrgId": "guid",
    "buyerEmail": "<buyer-contact-email>"
  },
  "sellerNotification": {
    "requested": true,
    "submitted": true,
    "notificationId": "guid",
    "notificationStatus": "sent",
    "sellerAccessLinkId": "guid",
    "sellerPortalUrl": "<configured-buyer-portal-url>/<seller-token>",
    "expiresAtUtc": "2026-08-21T00:00:00Z",
    "sellerContactId": "guid",
    "sellerOrgId": "guid",
    "sellerEmail": "<seller-contact-email>"
  }
}
```

If notification submission fails after the lien is confirmed, the lien transition remains committed and
`notification.submitted=false` reports the buyer-email failure for retry. The seller email is skipped unless the buyer
email is submitted or already submitted; in that case `sellerNotification.notificationStatus` is `skipped`. If seller
email submission itself fails, `sellerNotification.submitted=false` reports the failure without rolling back the lien
transition or buyer notification.

### GET `/api/liens/selling/public/{token}`

Returns the temporary funding-company or seller-view portal data opened from a `New Lien Offer` email CTA. This endpoint
is anonymous; the opaque token controls tenant, lien, buyer contact, expiry, revocation, and audience. It does not
render HTML. The tenant portal route `/selling/public/{token}` fetches this JSON through the gateway and owns the UI
rendering.

**Authentication:** None.

**Response:** `200 OK`, `application/json`

The JSON payload is populated only from persisted lien, case, contact, buyer, seller, access-link, and servicing
document metadata. It includes seller, buyer/funding company, lien summary, case, access-link expiry, and real
supporting-document fields. It never inserts sample company names, sample people, sample files, `example.com`, or
caller-provided CTA data.

```json
{
  "audience": "buyer",
  "accessLink": {
    "createdAtUtc": "2026-07-23T13:59:57.67655Z",
    "expiresAtUtc": "2026-08-22T13:59:57.67655Z",
    "lastAccessedAtUtc": "2026-07-23T14:01:00Z",
    "notificationSubmittedAtUtc": "2026-07-23T13:59:58Z",
    "responseStatus": null,
    "responseAmount": null,
    "responseNotes": null,
    "respondedAtUtc": null
  },
  "lien": {
    "id": "guid",
    "lienCode": "LIEN-001",
    "status": "Offered",
    "sellerStatus": "SubmittedForSale",
    "submittedAtUtc": "2026-07-23T13:59:57.67655Z",
    "listingVisibility": "Private",
    "initialServiceDate": "2026-01-12",
    "endServiceDate": "2026-02-14",
    "originalAmount": 24850.00,
    "askAmount": 21000.00,
    "offerPrice": 21000.00,
    "notes": "Persisted lien notes"
  },
  "seller": {
    "name": "Seller display name",
    "company": "Seller company",
    "email": "seller@company.test"
  },
  "buyer": {
    "contactName": "Buyer contact",
    "company": "Funding company",
    "email": "buyer@company.test",
    "phone": "3105551212"
  },
  "case": {
    "handlingLawFirm": "Handling law firm",
    "caseManager": "Case manager"
  },
  "documents": [
    {
      "fileName": "real-document.pdf",
      "category": "Lien Document",
      "sizeOrType": "PDF"
    }
  ]
}
```

For seller-view links, `audience` is `seller`; the same JSON includes buyer/funding-company details for read-only
review, but response and activation endpoints reject that token with `403 read-only-link`.

### POST `/api/liens/selling/public/{token}/activate-account`

Creates or links a buyer portal account for the token-scoped buyer organization. This endpoint is anonymous, uses the
same token validation as the public `GET`, and is intended to be called by the tenant portal BFF path
`/api/lien/api/liens/selling/public/{token}/activate-account`. Liens asks Identity to create or resolve a tenant-scoped
`LIEN_OWNER` organization for the source Liens buyer organization id, then Identity grants `SYNQ_LIENS` product access
and assigns `SYNQLIEN_BUYER` scoped to that Identity organization. Existing buyer contact values from the token win over
editable request values; request values only fill missing contact data.

This account activation does not accept or decline the lien, create a Bill of Sale, mark a lien sold, or otherwise
finalize sale. Seller-view tokens are read-only and return `403 read-only-link`.

**Authentication:** None.

**Request:**

```json
{
  "companyName": "Funding company",
  "email": "buyer@company.test",
  "firstName": "Buyer",
  "lastName": "Contact",
  "phone": "3105551212",
  "password": "chosen-password"
}
```

**Response:** `200 OK`

```json
{
  "userId": "guid",
  "isNew": true,
  "loginUrl": "/login?returnTo=%2Ffunding%2Foffered-liens&reason=synqlien-buyer-activation"
}
```

### POST `/api/liens/selling/public/{token}/accept`

Compatibility alias: `POST /api/liens/selling/public/{token}/offers`.

Records an accepted buyer response for the token-scoped lien. This is anonymous and uses the same token validation as
the public `GET`: missing or unknown tokens return `404`, revoked or expired tokens return `410`, and contradictory
repeat responses return `409`. Accepting records the current ask amount on the access link and moves the lien lifecycle
status from `Offered` to `Accepted` with `SellerStatus=Accepted`; it does not create a Bill of Sale, mark the lien sold,
or finalize sale. Seller-view tokens are read-only and return `403 read-only-link`. The
`/offers` alias accepts the same response shape; legacy `message` fields are stored as response notes. The first
accepted response submits `lien.offer.accepted` emails to both the buyer and seller through Notifications with
recipient-specific idempotency keys. Repeated same-response posts return the recorded response and retry those
idempotent notification submissions, so transient failures can recover without duplicate emails. Notification submission
failures are logged and do not roll back the recorded buyer response. The email subject is exactly
`Lien Offer Accepted`, and the email includes a pre-rendered HTML body. Liens does not supply a notification template key
for this outcome email, so template rendering cannot override the fixed subject or HTML design.
Liens must be configured with `NotificationsService:BaseUrl` (or legacy `Services:NotificationsUrl`) and the shared
service-token signing key through `FLOW_SERVICE_TOKEN_SECRET` or `ServiceTokens:SigningKey`, because Notifications
requires service JWT auth for producer submissions.

**Authentication:** None.

**Headers:**

| Header | Required | Notes |
|---|---|---|
| `Idempotency-Key` | No | Stored with the access-link response for replay/audit correlation |

**Request:**

```json
{
  "notes": "Accepted at ask"
}
```

**Response:** `200 OK`, same JSON shape as `GET /api/liens/selling/public/{token}`, with:

```json
{
  "accessLink": {
    "responseStatus": "Accepted",
    "responseAmount": 2500.00,
    "responseNotes": "Accepted at ask",
    "respondedAtUtc": "2026-07-23T14:10:00Z"
  },
  "lien": {
    "status": "Accepted",
    "sellerStatus": "Accepted"
  }
}
```

### POST `/api/liens/selling/public/{token}/decline`

Records a declined buyer response for the token-scoped lien. This is anonymous and uses the same token validation and
conflict behavior as public accept. Declining can record an optional reason and marks the offered lien with
`Status=Declined` and `SellerStatus=Declined`; it does not mark the lien sold, withdraw the seller listing, or create a
Bill of Sale. Seller-view tokens are read-only and return `403 read-only-link`. The first declined response submits
`lien.offer.rejected` emails to both the buyer and seller through Notifications with recipient-specific idempotency
keys. Repeated same-response posts return the recorded response and retry those idempotent notification submissions, so
transient failures can recover without duplicate emails. Notification submission failures are logged and do not roll back
the recorded buyer response. The email subject is exactly `Lien Offer Declined`, and the email includes a pre-rendered
HTML body. Liens does not supply a notification template key for this outcome email, so template rendering cannot
override the fixed subject or HTML design.
Liens must be configured with `NotificationsService:BaseUrl` (or legacy `Services:NotificationsUrl`) and the shared
service-token signing key through `FLOW_SERVICE_TOKEN_SECRET` or `ServiceTokens:SigningKey`, because Notifications
requires service JWT auth for producer submissions.

**Authentication:** None.

**Request:**

```json
{
  "reason": "Not in buying criteria"
}
```

**Response:** `200 OK`, same JSON shape as `GET /api/liens/selling/public/{token}`, with:

```json
{
  "accessLink": {
    "responseStatus": "Declined",
    "responseAmount": null,
    "responseNotes": "Not in buying criteria",
    "respondedAtUtc": "2026-07-23T14:10:00Z"
  },
  "lien": {
    "status": "Declined",
    "sellerStatus": "Declined"
  }
}
```

**Errors:**

| Status | Description |
|---|---|
| `404 Not Found` | Token or linked lien data cannot be resolved |
| `403 Forbidden` | Token is a seller read-only link and cannot record buyer actions |
| `410 Gone` | Token is expired or revoked |
| `409 Conflict` | Lien is no longer actionable, ask amount is unavailable, or a different response was already recorded |

---

## Cases Endpoints

Base path: `/api/liens/cases`

### GET `/api/liens/cases`

Search and list cases with optional filters.

**Permission:** `SYNQ_LIENS.case:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by case status |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<CaseResponse>
```

---

### GET `/api/liens/cases/{id}`

Get a case by its unique identifier.

**Permission:** `SYNQ_LIENS.case:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Case unique identifier |

**Response:** `200 OK` — `CaseResponse`

**Error:** `404 Not Found` — if the case does not exist.

---

### GET `/api/liens/cases/by-number/{caseNumber}`

Get a case by its case number.

**Permission:** `SYNQ_LIENS.case:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `caseNumber` | `string` | Case number |

**Response:** `200 OK` — `CaseResponse`

**Error:** `404 Not Found` — if the case does not exist.

---

### POST `/api/liens/cases`

Create a new case.

**Permission:** `SYNQ_LIENS.case:create`

**Request Body: `CreateCaseRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `caseNumber` | `string` | Yes | No | Unique case number |
| `clientFirstName` | `string` | Yes | No | Client first name |
| `clientLastName` | `string` | Yes | No | Client last name |
| `externalReference` | `string` | No | Yes | External reference identifier |
| `title` | `string` | No | Yes | Case title |
| `clientDob` | `date` | No | Yes | Client date of birth (format: `YYYY-MM-DD`) |
| `clientPhone` | `string` | No | Yes | Client phone number |
| `clientEmail` | `string` | No | Yes | Client email address |
| `clientAddress` | `string` | No | Yes | Client address |
| `dateOfIncident` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `insuranceCarrier` | `string` | No | Yes | Insurance carrier name |
| `policyNumber` | `string` | No | Yes | Insurance policy number |
| `claimNumber` | `string` | No | Yes | Insurance claim number |
| `description` | `string` | No | Yes | Case description |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `201 Created` — `CaseResponse`

Returns the created case with a `Location` header pointing to `/api/liens/cases/{id}`.
Creation also adds a `Case Created` entry to the legacy case-update history endpoint (`POST /api/liens/cases/case-updates/v3`), including the case code, client, status, law firm, manager, and creator.

---

### PUT `/api/liens/cases/{id}`

Update an existing case.

**Permission:** `SYNQ_LIENS.case:update`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Case unique identifier |

**Request Body: `UpdateCaseRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `clientFirstName` | `string` | Yes | No | Client first name |
| `clientLastName` | `string` | Yes | No | Client last name |
| `externalReference` | `string` | No | Yes | External reference identifier |
| `title` | `string` | No | Yes | Case title |
| `clientDob` | `date` | No | Yes | Client date of birth (format: `YYYY-MM-DD`) |
| `clientPhone` | `string` | No | Yes | Client phone number |
| `clientEmail` | `string` | No | Yes | Client email address |
| `clientAddress` | `string` | No | Yes | Client address |
| `dateOfIncident` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `insuranceCarrier` | `string` | No | Yes | Insurance carrier name |
| `policyNumber` | `string` | No | Yes | Insurance policy number |
| `claimNumber` | `string` | No | Yes | Insurance claim number |
| `description` | `string` | No | Yes | Case description |
| `notes` | `string` | No | Yes | Additional notes |
| `status` | `string` | No | Yes | Case status |
| `demandAmount` | `decimal` | No | Yes | Demand amount |
| `settlementAmount` | `decimal` | No | Yes | Settlement amount |

**Response:** `200 OK` — `CaseResponse`

**Error:** `404 Not Found` — if the case does not exist.

---

### GET `/api/liens/cases/notes/{caseId}`

Return the legacy case-note history. Each changed non-empty `notes` value submitted through `PATCH /api/liens/cases/details-update` is appended as a new case-note entry rather than replacing prior entries. Feed notes and system update-history entries are intentionally excluded.

**Permission:** `SYNQ_LIENS.case:read`

The response uses the legacy envelope `{ isSuccess, message, data }`. `data` is ordered newest first and each item includes the historical `note` value and creator metadata. `created` is the U.S. Pacific display string, while `createdAtUtc` is the corresponding canonical UTC ISO timestamp.

`POST /api/liens/cases/add-note` and `POST /api/liens/cases/get-notes` are the separate Feed-note routes. Feed notes are shown only in the case Feed; they are not returned by this case-notes endpoint or by case-update history.

---

### POST `/api/liens/cases/dashboard/deployed` and `/api/liens/cases/dashboard/cash-received`

Return dashboard totals for deployed liens and cash received. Supplying both `startDate` and `endDate` filters the metric to that inclusive range. When neither date is supplied, the metric includes all available tenant history; `periodStart` and `periodEnd` are returned as empty strings to indicate the all-time result.

The dashboard Total Lien Report, including its status chart and totals, excludes `Rejected` and `Cancelled` liens before aggregation and pagination.

---

### POST `/api/liens/cases/upload/document`

Legacy-compatible case document upload endpoint.

**Permission:** `SYNQ_LIENS.case:update`

**Content-Type:** `multipart/form-data`

**Form Fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `file` | file | Yes | Document file. Allowed extensions: `.pdf`, `.jpg`, `.jpeg`, `.png`, `.docx`, `.xlsx`, `.xls`, `.csv`. Maximum size: 50 MB. |
| `caseId` | `guid` | Yes | Case identifier to link the uploaded document to. |
| `DocFileTypeId` | `string` | No | Legacy document type ID. Preserved in local metadata; UUID values are forwarded to Documents as `documentTypeId`. |
| `DocName` | `string` | No | Document title. Defaults to the uploaded filename without extension. |
| `DocDescription` | `string` | No | Document description. Defaults to the file extension label. |

Uploads the file to the Documents service and records legacy document metadata as a `LegacyCaseDocument` servicing item for compatibility with existing case document lookups.

**Response:** `200 OK`

```json
{
  "isSuccess": true,
  "message": "Successfully uploaded document.",
  "data": {
    "url": "/documents/{documentId}",
    "documentId": "guid"
  }
}
```

---

### POST `/api/liens/cases/liens/upload/document`

Legacy-compatible lien document upload endpoint.

**Permission:** `SYNQ_LIENS.lien:update`

**Content-Type:** `multipart/form-data`

**Form Fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `file` | file | Yes | Document file. Allowed extensions: `.pdf`, `.jpg`, `.jpeg`, `.png`, `.docx`, `.xlsx`, `.xls`, `.csv`. Maximum size: 50 MB. |
| `liensId` | `guid` | Yes | Lien identifier to link the uploaded document to. `lienId` is also accepted. |
| `DocFileTypeId` | `string` | No | Legacy document type ID. Preserved in local metadata; UUID values are forwarded to Documents as `documentTypeId`. |
| `DocName` | `string` | No | Document title. Defaults to the uploaded filename without extension. |
| `DocDescription` | `string` | No | Document description. Defaults to the file extension label. |

Uploads the file to the Documents service and records legacy document metadata as a `LegacyLienDocument` servicing item.

**Response:** `200 OK`

```json
{
  "isSuccess": true,
  "message": "Successfully uploaded document.",
  "data": {
    "url": "/documents/{documentId}",
    "documentId": "guid"
  }
}
```

---

### CaseResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `caseNumber` | `string` | No | Case number |
| `externalReference` | `string` | Yes | External reference |
| `title` | `string` | Yes | Case title |
| `clientFirstName` | `string` | No | Client first name |
| `clientLastName` | `string` | No | Client last name |
| `clientDisplayName` | `string` | No | Computed client display name |
| `status` | `string` | No | Current status |
| `dateOfIncident` | `date` | Yes | Date of incident |
| `clientDob` | `date` | Yes | Client date of birth |
| `clientPhone` | `string` | Yes | Client phone number |
| `clientEmail` | `string` | Yes | Client email address |
| `clientAddress` | `string` | Yes | Client address |
| `insuranceCarrier` | `string` | Yes | Insurance carrier name |
| `policyNumber` | `string` | Yes | Insurance policy number |
| `claimNumber` | `string` | Yes | Insurance claim number |
| `demandAmount` | `decimal` | Yes | Demand amount |
| `settlementAmount` | `decimal` | Yes | Settlement amount |
| `description` | `string` | Yes | Case description |
| `notes` | `string` | Yes | Additional notes |
| `openedAtUtc` | `datetime` | Yes | When the case was opened |
| `closedAtUtc` | `datetime` | Yes | When the case was closed |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Bills of Sale Endpoints

Base path: `/api/liens/bill-of-sales`

### GET `/api/liens/bill-of-sales`

Search and list bills of sale with optional filters.

**Permission:** `SYNQ_LIENS.lien:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by bill of sale status |
| `lienId` | `guid` | No | `null` | Filter by associated lien ID |
| `sellerOrgId` | `guid` | No | `null` | Filter by seller organization ID |
| `buyerOrgId` | `guid` | No | `null` | Filter by buyer organization ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<BillOfSaleResponse>
```

---

### GET `/api/liens/bill-of-sales/{id}`

Get a bill of sale by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### GET `/api/liens/bill-of-sales/by-number/{billOfSaleNumber}`

Get a bill of sale by its bill of sale number.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `billOfSaleNumber` | `string` | Bill of sale number |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### GET `/api/liens/liens/{lienId}/bill-of-sales`

Get all bills of sale associated with a specific lien.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `lienId` | `guid` | Lien unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse[]`

---

### GET `/api/liens/bill-of-sales/{id}/document`

Download the document file for a bill of sale by its ID.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — Binary file download with appropriate `Content-Type` and `Content-Disposition` headers.

---

### GET `/api/liens/bill-of-sales/by-number/{billOfSaleNumber}/document`

Download the document file for a bill of sale by its number.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `billOfSaleNumber` | `string` | Bill of sale number |

**Response:** `200 OK` — Binary file download with appropriate `Content-Type` and `Content-Disposition` headers.

---

### PUT `/api/liens/bill-of-sales/{id}/submit`

Submit a bill of sale for execution.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### PUT `/api/liens/bill-of-sales/{id}/execute`

Execute a bill of sale.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### PUT `/api/liens/bill-of-sales/{id}/cancel`

Cancel a bill of sale.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `reason` | `string` | No | `null` | Reason for cancellation |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### BillOfSaleResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `billOfSaleNumber` | `string` | No | Bill of sale number |
| `externalReference` | `string` | Yes | External reference |
| `status` | `string` | No | Current status |
| `lienId` | `guid` | No | Associated lien ID |
| `lienOfferId` | `guid` | No | Associated lien offer ID |
| `sellerOrgId` | `guid` | No | Seller organization ID |
| `buyerOrgId` | `guid` | No | Buyer organization ID |
| `purchaseAmount` | `decimal` | No | Purchase amount |
| `originalLienAmount` | `decimal` | No | Original lien amount |
| `discountPercent` | `decimal` | Yes | Discount percentage |
| `sellerContactName` | `string` | Yes | Seller contact name |
| `buyerContactName` | `string` | Yes | Buyer contact name |
| `terms` | `string` | Yes | Terms of sale |
| `notes` | `string` | Yes | Additional notes |
| `documentId` | `guid` | Yes | Associated document ID |
| `issuedAtUtc` | `datetime` | No | When the bill of sale was issued |
| `executedAtUtc` | `datetime` | Yes | When executed |
| `effectiveAtUtc` | `datetime` | Yes | When effective |
| `cancelledAtUtc` | `datetime` | Yes | When cancelled |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Lien Offers Endpoints

Base path: `/api/liens/offers`

### GET `/api/liens/offers`

Search and list lien offers with optional filters.

**Permission:** `SYNQ_LIENS.lien:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `lienId` | `guid` | No | `null` | Filter by lien ID |
| `status` | `string` | No | `null` | Filter by offer status |
| `buyerOrgId` | `guid` | No | `null` | Filter by buyer organization ID |
| `sellerOrgId` | `guid` | No | `null` | Filter by seller organization ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<LienOfferResponse>
```

---

### GET `/api/liens/offers/{id}`

Get a lien offer by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Lien offer unique identifier |

**Response:** `200 OK` — `LienOfferResponse`

**Error:** `404 Not Found` — if the lien offer does not exist.

---

### GET `/api/liens/liens/{lienId}/offers`

Get all offers associated with a specific lien.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `lienId` | `guid` | Lien unique identifier |

**Response:** `200 OK` — `LienOfferResponse[]`

---

### POST `/api/liens/offers`

Create a new lien offer.

**Permission:** `SYNQ_LIENS.lien:offer`

**Request Body: `CreateLienOfferRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `lienId` | `guid` | Yes | No | ID of the lien being offered on |
| `offerAmount` | `decimal` | Yes | No | Offer amount |
| `notes` | `string` | No | Yes | Additional notes |
| `expiresAtUtc` | `datetime` | No | Yes | Offer expiration date/time (UTC) |

**Response:** `201 Created` — `LienOfferResponse`

Returns the created offer with a `Location` header pointing to `/api/liens/offers/{id}`.

---

### POST `/api/liens/offers/{offerId}/accept`

Accept a lien offer.

**Permission:** `SYNQ_LIENS.lien:update`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `offerId` | `guid` | Lien offer unique identifier |

**Response:** `200 OK` — `SaleFinalizationResult`

**Error:** `404 Not Found` — if the lien offer does not exist.

---

### LienOfferResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `lienId` | `guid` | No | Associated lien ID |
| `offerAmount` | `decimal` | No | Offer amount |
| `status` | `string` | No | Current status |
| `buyerOrgId` | `guid` | No | Buyer organization ID |
| `sellerOrgId` | `guid` | No | Seller organization ID |
| `notes` | `string` | Yes | Offer notes |
| `responseNotes` | `string` | Yes | Response notes from the seller |
| `externalReference` | `string` | Yes | External reference |
| `offeredAtUtc` | `datetime` | No | When the offer was made |
| `expiresAtUtc` | `datetime` | Yes | When the offer expires |
| `respondedAtUtc` | `datetime` | Yes | When the offer was responded to |
| `withdrawnAtUtc` | `datetime` | Yes | When the offer was withdrawn |
| `isExpired` | `boolean` | No | Whether the offer has expired |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

### SaleFinalizationResult

Returned when a lien offer is accepted. Contains details about the finalized sale.

| Field | Type | Nullable | Description |
|---|---|---|---|
| `acceptedOfferId` | `guid` | No | ID of the accepted offer |
| `acceptedOfferStatus` | `string` | No | Final status of the accepted offer |
| `lienId` | `guid` | No | ID of the lien involved in the sale |
| `finalLienStatus` | `string` | No | Final status of the lien after sale |
| `billOfSaleId` | `guid` | No | ID of the generated bill of sale |
| `billOfSaleNumber` | `string` | No | Number of the generated bill of sale |
| `billOfSaleStatus` | `string` | No | Status of the generated bill of sale |
| `purchaseAmount` | `decimal` | No | Purchase amount |
| `originalLienAmount` | `decimal` | No | Original lien amount |
| `discountPercent` | `decimal` | Yes | Discount percentage |
| `documentId` | `guid` | Yes | Associated document ID |
| `competingOffersRejected` | `integer` | No | Number of competing offers that were rejected |
| `finalizedAtUtc` | `datetime` | No | When the sale was finalized |

---

## Contacts Endpoints

Base path: `/api/liens/contacts`

### GET `/api/liens/contacts`

Search and list contacts with optional filters.

**Permission:** `SYNQ_LIENS.lien:service`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `contactType` | `string` | No | `null` | Filter by contact type |
| `isActive` | `boolean` | No | `null` | Filter by active status |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<ContactResponse>
```

---

### GET `/api/liens/contacts/{id}`

Get a contact by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### POST `/api/liens/contacts`

Create a new contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Request Body: `CreateContactRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `contactType` | `string` | Yes | No | Type of contact |
| `firstName` | `string` | Yes | No | First name |
| `lastName` | `string` | Yes | No | Last name |
| `title` | `string` | No | Yes | Job title |
| `organization` | `string` | No | Yes | Organization name |
| `email` | `string` | No | Yes | Email address |
| `phone` | `string` | No | Yes | Phone number |
| `fax` | `string` | No | Yes | Fax number |
| `website` | `string` | No | Yes | Website URL |
| `addressLine1` | `string` | No | Yes | Street address |
| `city` | `string` | No | Yes | City |
| `state` | `string` | No | Yes | State |
| `postalCode` | `string` | No | Yes | Postal code |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `201 Created` — `ContactResponse`

Returns the created contact with a `Location` header pointing to `/api/liens/contacts/{id}`.

---

### PUT `/api/liens/contacts/{id}`

Update an existing contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Request Body: `UpdateContactRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `contactType` | `string` | Yes | No | Type of contact |
| `firstName` | `string` | Yes | No | First name |
| `lastName` | `string` | Yes | No | Last name |
| `title` | `string` | No | Yes | Job title |
| `organization` | `string` | No | Yes | Organization name |
| `email` | `string` | No | Yes | Email address |
| `phone` | `string` | No | Yes | Phone number |
| `fax` | `string` | No | Yes | Fax number |
| `website` | `string` | No | Yes | Website URL |
| `addressLine1` | `string` | No | Yes | Street address |
| `city` | `string` | No | Yes | City |
| `state` | `string` | No | Yes | State |
| `postalCode` | `string` | No | Yes | Postal code |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### PUT `/api/liens/contacts/{id}/deactivate`

Deactivate a contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### PUT `/api/liens/contacts/{id}/reactivate`

Reactivate a previously deactivated contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### ContactResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `contactType` | `string` | No | Type of contact |
| `firstName` | `string` | No | First name |
| `lastName` | `string` | No | Last name |
| `displayName` | `string` | No | Computed display name |
| `title` | `string` | Yes | Job title |
| `organization` | `string` | Yes | Organization name |
| `email` | `string` | Yes | Email address |
| `phone` | `string` | Yes | Phone number |
| `fax` | `string` | Yes | Fax number |
| `website` | `string` | Yes | Website URL |
| `addressLine1` | `string` | Yes | Street address |
| `city` | `string` | Yes | City |
| `state` | `string` | Yes | State |
| `postalCode` | `string` | Yes | Postal code |
| `notes` | `string` | Yes | Additional notes |
| `isActive` | `boolean` | No | Whether the contact is active |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Servicing Endpoints

Base path: `/api/liens/servicing`

### GET `/api/liens/servicing`

Search and list servicing items with optional filters.

**Permission:** `SYNQ_LIENS.lien:service`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by status |
| `priority` | `string` | No | `null` | Filter by priority |
| `assignedTo` | `string` | No | `null` | Filter by assignee |
| `caseId` | `guid` | No | `null` | Filter by associated case ID |
| `lienId` | `guid` | No | `null` | Filter by associated lien ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<ServicingItemResponse>
```

---

### GET `/api/liens/servicing/{id}`

Get a servicing item by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Servicing item unique identifier |

**Response:** `200 OK` — `ServicingItemResponse`

**Error:** `404 Not Found` — if the servicing item does not exist.

---

### POST `/api/liens/servicing`

Create a new servicing item.

**Permission:** `SYNQ_LIENS.lien:service`

**Request Body: `CreateServicingItemRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `taskNumber` | `string` | Yes | No | Unique task number |
| `taskType` | `string` | Yes | No | Type of task |
| `description` | `string` | Yes | No | Task description |
| `assignedTo` | `string` | Yes | No | Name of assignee |
| `assignedToUserId` | `guid` | No | Yes | User ID of assignee |
| `priority` | `string` | No | Yes | Priority level |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `lienId` | `guid` | No | Yes | Associated lien ID |
| `dueDate` | `date` | No | Yes | Due date (format: `YYYY-MM-DD`) |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `201 Created` — `ServicingItemResponse`

Returns the created servicing item with a `Location` header pointing to `/api/liens/servicing/{id}`.

---

### PUT `/api/liens/servicing/{id}`

Update an existing servicing item.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Servicing item unique identifier |

**Request Body: `UpdateServicingItemRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `taskType` | `string` | Yes | No | Type of task |
| `description` | `string` | Yes | No | Task description |
| `assignedTo` | `string` | Yes | No | Name of assignee |
| `assignedToUserId` | `guid` | No | Yes | User ID of assignee |
| `priority` | `string` | No | Yes | Priority level |
| `status` | `string` | No | Yes | Status |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `lienId` | `guid` | No | Yes | Associated lien ID |
| `dueDate` | `date` | No | Yes | Due date (format: `YYYY-MM-DD`) |
| `notes` | `string` | No | Yes | Additional notes |
| `resolution` | `string` | No | Yes | Resolution notes |

**Response:** `200 OK` — `ServicingItemResponse`

**Error:** `404 Not Found` — if the servicing item does not exist.

---

### PUT `/api/liens/servicing/{id}/status`

Update the status of a servicing item.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Servicing item unique identifier |

**Request Body: `UpdateStatusRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `status` | `string` | Yes | No | New status value |
| `resolution` | `string` | No | Yes | Resolution notes |

**Response:** `200 OK` — `ServicingItemResponse`

**Error:** `404 Not Found` — if the servicing item does not exist.

---

### ServicingItemResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `taskNumber` | `string` | No | Task number |
| `taskType` | `string` | No | Type of task |
| `description` | `string` | No | Task description |
| `status` | `string` | No | Current status |
| `priority` | `string` | No | Priority level |
| `assignedTo` | `string` | No | Name of assignee |
| `assignedToUserId` | `guid` | Yes | User ID of assignee |
| `caseId` | `guid` | Yes | Associated case ID |
| `lienId` | `guid` | Yes | Associated lien ID |
| `dueDate` | `date` | Yes | Due date |
| `notes` | `string` | Yes | Additional notes |
| `resolution` | `string` | Yes | Resolution notes |
| `startedAtUtc` | `datetime` | Yes | When work was started |
| `completedAtUtc` | `datetime` | Yes | When work was completed |
| `escalatedAtUtc` | `datetime` | Yes | When item was escalated |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Reports Endpoints

### GET `/report/diy/columns`

Returns the legacy DIY-report column metadata and the ordered default selection for the requested report type. For `LIENS`, the default selection includes `days_since_reduction_approval` in position 9 (zero-based), followed by `case_status` and `date_of_loss` in positions 14 and 15 respectively. `initial_service_date` and `number_of_liens` remain available as optional columns but are not selected by default.
