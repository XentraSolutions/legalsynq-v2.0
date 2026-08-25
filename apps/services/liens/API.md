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
- [Settlement Reductions](#settlement-reduction-endpoints)
- [Settlement Payments](#settlement-payment-endpoints)
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
`LienResponse.purchaseDate` and `LienResponse.initialServiceDate` are formatted as `MM/dd/yyyy` when present.

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
| `sellingCaseId` | `guid` | Yes | Original Selling case ID when moved to Liens Management |
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
| `sellerStatus` | `string` | Yes | Selling workflow status, including `Internal` |
| `incidentDate` | `date` | Yes | Date of incident |
| `description` | `string` | Yes | Description |
| `openedAtUtc` | `datetime` | Yes | When the lien was opened |
| `closedAtUtc` | `datetime` | Yes | When the lien was closed |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Selling Endpoints

Base path: `/api/liens/selling`

### POST `/api/liens/selling/liens/{lienId}/move-to-management`

Moves a draft Selling lien into Liens Management without creating a second lien record. The lien must already
be linked to an existing same-tenant, same-organization case. That `caseId` remains unchanged and is preserved
in `sellingCaseId`; `sellerStatus` becomes `Internal`.

**Permission:** `SYNQ_LIENS.lien_sale:update`

**Header:** `Idempotency-Key` is required.

```json
{
  "reason": "Retained internally"
}
```

Only draft liens with Selling `Pending` or `Internal` status are eligible. A lien already submitted for sale
must first be withdrawn through `withdraw-sale`, which revokes buyer access and pending offers.

### POST `/api/liens/selling/liens/{lienId}/confirm-sale`

Confirms a prepared seller lien for sale. The endpoint moves a draft/prepared lien to `Offered` with
`SellerStatus=SubmittedForSale`, copies the persisted `AskAmount` into `OfferPrice`, and keeps `SoldAtUtc` null.

**Permission:** `SYNQ_LIENS.lien_sale:update`

**Headers:**

| Header | Required | Description |
|---|---|---|
| `Idempotency-Key` | No | Used with tenant/lien/buyer/seller contacts to suppress duplicate notification sends on replay |

**Request:**

```json
{
  "confirmationAccepted": true
}
```

Notification delivery is mandatory and cannot be opted out through request payload. The lien must have real
`FundingCompanyId`, `FundingCompanyContactId`, `InitialServiceDate`, `AskAmount`, buyer email, seller
organization display, active seller Company Directory contact-person data, seller notification email, and handling law firm data. Buyer-facing seller name is the
`idt_Users.FirstName` + `LastName` display name for the seller user who confirms/submits the offer
(`SellingBuyerAccessLinks.CreatedByUserId` / confirm-sale acting user), scoped to the seller organization when Identity
validates membership. Seller company represents the selling
organization (`sellerOrgId`) resolved from Identity, with fallback to active `liens_CompanyContactPersons` joined through
active `liens_Companies` in that seller organization. Handling law firm and case manager names stay in
the asset/case fields and are not used as the seller display. Handling law firm is the selected standalone law-firm
contact's `liens_Contacts.Organization` value, falling back to `DisplayName` for legacy or incomplete firm records. In
buyer and seller notification Asset Overview sections, Contact Person, Email Address, and Handling Law Firm all come
from that selected contact: `liens_Contacts.FirstName` + `liens_Contacts.LastName`, `liens_Contacts.Email`, and the
organization/display-name value. Creating a standalone law firm without a separate organization value persists its
display name as the organization.
The seller notification's Buyer Information section omits buyer phone number. The public-link JSON and authenticated funding-company
views use the same seller-user and seller organization resolver. The API creates a 30-day buyer response access link and a separate
30-day seller-view access link from
`Liens:Selling:BuyerPortalBaseUrl`; callers do not provide CTA URLs. If the explicit base URL is absent, the API
derives it from `SYNQLIEN_COMMON_PORTAL_HOSTNAME`; `synqlien-demo.localhost` resolves to
`http://synqlien-demo.localhost:5000/selling/public` for the full `scripts/run-dev.sh` proxy. The configured buyer
portal base URL must be absolute and must match the active tenant-web browser origin; use
`http://synqlien-demo.localhost:3000/selling/public` when running only `pnpm --dir apps/web dev`. Literal loopback hosts
such as `localhost` or `127.0.0.1` are rejected because the email CTA must work from the recipient's inbox, while named
`.localhost` aliases such as `synqlien-demo.localhost` are allowed for local demo runs. The buyer email uses the
`New Lien Offer` copy with a response CTA. The seller receives the same branded format with buyer/funding-company
information and a `View Lien Details` CTA. Neither email inserts sample
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
    "sellerEmail": "<seller-notification-email>"
  }
}
```

If notification submission fails after the lien is confirmed, the lien transition remains committed and
`notification.submitted=false` reports the buyer-email failure for retry. The seller email is skipped unless the buyer
email is submitted or already submitted; in that case `sellerNotification.notificationStatus` is `skipped`. If seller
email submission itself fails, `sellerNotification.submitted=false` reports the failure without rolling back the lien
transition or buyer notification.

### GET `/api/liens/selling/buyer/dashboard`

Returns the authenticated funding-company dashboard used by `/funding/dashboard`. The endpoint scopes data to active
tenant buyer contacts whose email matches the authenticated user and whose contact type is `FundingCompany` or
`LienHolder`, then includes only access links where `BuyerContactId` matches one of those contacts.

Summary metrics are buyer-scoped totals across the selected dashboard range:

| Field | Definition |
|---|---|
| `totalLienPendingCount` | Count of buyer access links with no buyer response |
| `totalLienPendingAmount` | Sum of original lien amounts for pending rows |
| `totalPendingOfferCount` | Count of pending buyer offers |
| `totalPendingOfferedAmount` | Sum of pending ask/response offer amounts |
| `purchasedLienCount` | Count of accepted buyer responses |
| `capitalDeployedAmount` | Sum of accepted response amounts, falling back to ask amount |

`summary.trends` contains one trend per KPI card: `totalLienPending`, `totalPendingOffered`, `purchasedLiens`, and
`capitalDeployed`. Each trend compares current calendar month activity with the previous full calendar month and returns
`value` as the absolute percent delta, `direction` as `up`, `down`, or `flat`, and `label` as the previous-month range
shown by the portal.

`range=last7Days|last30Days|custom`, `from=yyyy-MM-dd`, and `to=yyyy-MM-dd` filter summary metrics, pending offers,
acquisition pipeline stages, provider performance, and offer inbox data. Range filtering uses the offer received
timestamp for pending, accepted, and declined rows so the dashboard matches the offered-liens received date. Custom
ranges require both `from` and `to`; missing or invalid custom dates return empty dashboard data.

`pendingOffers` returns at most five pending offers for the dashboard preview within the selected range.
`providerPerformance` returns at most five provider groups within the selected range, ordered by highest `lienCount`
first and then by `providerName`.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Response:** `200 OK`

```json
{
  "summary": {
    "totalLienPendingCount": 1,
    "totalLienPendingAmount": 9000.00,
    "totalPendingOfferCount": 1,
    "totalPendingOfferedAmount": 2500.00,
    "purchasedLienCount": 1,
    "capitalDeployedAmount": 2500.00,
    "trends": {
      "totalLienPending": {
        "value": 8.9,
        "direction": "up",
        "label": "vs Apr 1 - Apr 30"
      },
      "totalPendingOffered": {
        "value": 6.4,
        "direction": "up",
        "label": "vs Apr 1 - Apr 30"
      },
      "purchasedLiens": {
        "value": 14.2,
        "direction": "up",
        "label": "vs Apr 1 - Apr 30"
      },
      "capitalDeployed": {
        "value": 5.0,
        "direction": "down",
        "label": "vs Apr 1 - Apr 30"
      }
    }
  },
  "pendingOffers": [
    {
      "id": "access-link-guid",
      "lienNumber": "LIEN-001",
      "providerName": "Sunrise Clinic",
      "sellerCompany": "RL Liens1",
      "sellerName": "Seller Processor",
      "offeredAmount": 2500.00,
      "receivedAtUtc": "2026-07-28T12:00:00Z",
      "responseDueAtUtc": "2026-08-27T12:00:00Z",
      "status": "Pending",
      "detailHref": "/funding/offered-liens/<access-link-guid>"
    }
  ],
  "pipelineStages": [
    {
      "key": "pending",
      "label": "Pending",
      "count": 1,
      "totalAmount": 2500.00,
      "conversionRatePercent": null
    }
  ],
  "providerPerformance": [
    {
      "providerId": "facility-guid",
      "providerName": "Sunrise Clinic",
      "lienCount": 2,
      "offeredAmount": 5000.00,
      "acceptedAmount": 2500.00,
      "averageResponseHours": 4.5
    }
  ],
  "offerInbox": {
    "pendingCount": 1,
    "unreadCount": 0,
    "latestReceivedAtUtc": "2026-07-28T12:00:00Z"
  }
}
```

### GET `/api/liens/selling/buyer/liens`

Returns offered-liens rows for the authenticated SynqLien buyer/funding company. The endpoint reads confirmed buyer
access links created by seller confirm-sale notifications and scopes results to active tenant buyer contacts whose email
matches the authenticated user and whose contact type is `FundingCompany` or `LienHolder`. Only access links where
`BuyerContactId` matches one of those contacts are returned, which supports accounts provisioned from public buyer
activation without exposing another contact's offers from the same buyer organization.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `status` | `string` | No | `null` | `Pending`, `Accepted`, or `Declined`; omit or use `All` for every status |
| `search` | `string` | No | `null` | Case-insensitive search across lien number, seller, provider, status, dates, amounts, external reference, and subject name |
| `page` | `integer` | No | `1` | 1-based page number |
| `pageSize` | `integer` | No | `10` | Items per page, clamped from 1 to 100 |
| `sort` | `string` | No | `receivedAtUtc` | `lienNumber`, `sellerName`, `initialServiceDate`, `billingAmount`, `askAmount`, `highestBidAmount`, or `status` |
| `direction` | `string` | No | `asc` | `asc` or `desc`; default endpoint ordering is newest received offer first when `sort` is omitted |

**Response:** `200 OK`

```json
{
  "rows": [
    {
      "id": "access-link-guid",
      "lienNumber": "LIEN-001",
      "providerName": "Sunrise Clinic",
      "sellerName": "Seller Processor",
      "initialServiceDate": "2026-05-01",
      "serviceDate": "2026-05-01",
      "billingAmount": 9000.00,
      "originalAmount": 9000.00,
      "askAmount": 2500.00,
      "highestBidAmount": null,
      "highestBid": null,
      "offeredAmount": 2500.00,
      "receivedAtUtc": "2026-07-28T12:00:00Z",
      "status": "Pending",
      "responseDueAtUtc": "2026-08-27T12:00:00Z",
      "allowedActions": ["view", "accept", "decline"],
      "detailHref": "/funding/offered-liens/<access-link-guid>"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "total": 1
}
```

`status` is derived from `SellingBuyerAccessLinks.ResponseStatus`: missing response is `Pending`, accepted responses are
`Accepted`, and declined responses are `Declined`. Pending rows expose `view`, `accept`, and `decline` actions only
while the underlying lien remains actionable by the same public buyer-response rules; responded or otherwise
non-actionable rows expose `view` only.

### GET `/api/liens/selling/buyer/liens/{accessLinkId}`

Returns the authenticated funding-company detail view for one offered lien. The `{accessLinkId}` is the `id` returned
by `GET /api/liens/selling/buyer/liens`; access is scoped to the authenticated buyer contact matched by email, using the
same `BuyerContactId` filtering as the list endpoint.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Response:** `200 OK`

```json
{
  "id": "access-link-guid",
  "lienId": "lien-guid",
  "lienNumber": "LIEN-001",
  "title": "Seller Processor",
  "subtitle": "RL Liens1",
  "seller": {
    "name": "Seller Processor",
    "company": "RL Liens1",
    "email": null
  },
  "buyer": {
    "contactName": "Buyer Reviewer",
    "company": "Capital Fund LLC",
    "email": "buyer@capital.test",
    "phone": "3105551212"
  },
  "providerName": "Sunrise Clinic",
  "status": "Pending",
  "submittedAtUtc": "2026-07-28T12:00:00Z",
  "initialServiceDate": "2026-05-01",
  "endServiceDate": "2026-05-31",
  "billingAmount": 9000.00,
  "askAmount": 2500.00,
  "highestBidAmount": null,
  "responseAmount": null,
  "notes": "Persisted lien notes",
  "responseDueAtUtc": "2026-08-27T12:00:00Z",
  "responseStatus": null,
  "responseNotes": null,
  "respondedAtUtc": null,
  "allowedActions": ["view", "accept", "decline"],
  "documents": [
    {
      "id": "servicing-item-guid",
      "fileName": "signed-lien.pdf",
      "category": "Lien Document",
      "sizeOrType": "PDF",
      "url": "/documents/document-guid",
      "viewUrl": "/api/lien/api/liens/selling/buyer/liens/{access-link-guid}/documents/{document-guid}/view",
      "downloadUrl": "/api/lien/api/liens/selling/buyer/liens/{access-link-guid}/documents/{document-guid}/download",
      "createdAtUtc": "2026-07-28T12:00:00Z"
    }
  ],
  "messages": [
    {
      "id": "message-guid",
      "senderType": "buyer",
      "senderName": "Buyer Reviewer",
      "senderInitials": "BR",
      "senderEmail": "buyer@capital.test",
      "message": "Please review the signed lien package.",
      "createdAtUtc": "2026-07-28T12:00:00Z",
      "isCurrentUser": true
    }
  ],
  "activity": [
    {
      "id": "accesslinkguid-response",
      "label": "Pending -> Accepted",
      "occurredAtUtc": "2026-07-28T13:00:00Z",
      "notes": "Accepted after review"
    }
  ]
}
```

`documents`, `messages`, and `activity` are returned only from persisted records. They are empty arrays when no matching
servicing documents, portal messages, or buyer response activity exist. `allowedActions` exposes `accept` and `decline`
only when the access link has not recorded a response and the lien itself is still actionable. `viewUrl` and
`downloadUrl` are same-origin tenant-portal BFF paths for authenticated funding-portal document access. They are `null`
when the servicing item does not contain a resolvable Documents-service id.

### GET `/api/liens/selling/buyer/liens/{accessLinkId}/documents/{documentId}/view`

Issues a short-lived Documents view access token for a document attached to an authenticated offered lien, then
redirects to the Documents access route. The endpoint validates the same buyer-contact-scoped access link as the detail
endpoint before minting the Documents token. Documents not attached to the offered lien return
`404 document_not_found`.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Response:** `302 Found`

`Location` points to `/documents/access/{accessToken}` when called through the gateway. The tenant portal BFF path
`/api/lien/api/liens/selling/buyer/liens/{accessLinkId}/documents/{documentId}/view` rewrites that redirect to
`/api/lien/documents/access/{accessToken}` for same-origin browser access.

### GET `/api/liens/selling/buyer/liens/{accessLinkId}/documents/{documentId}/download`

Same validation and ownership checks as the authenticated offered-lien document view endpoint, but requests a Documents
download access token.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Response:** `302 Found`

### POST `/api/liens/selling/buyer/liens/{accessLinkId}/messages`

Posts a message from the authenticated funding-company detail page into the same persisted offer thread used by
`POST /api/liens/selling/public/{token}/messages`. The endpoint first resolves `{accessLinkId}` with the same
buyer-contact scoping as the detail `GET`, then delegates to the public-link message workflow so both the public email
link and `/funding/offered-liens/{accessLinkId}?tab=messages` show the same messages and notification behavior.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Request Body:**

```json
{
  "message": "Please review the signed lien package."
}
```

Messages are trimmed, required, and limited to 400 characters.

**Response:** `201 Created`, same message shape as the public message endpoint.

### POST `/api/liens/selling/buyer/liens/{accessLinkId}/accept`

Records an accepted buyer response from the authenticated funding-company detail page. The endpoint resolves
`{accessLinkId}` with authenticated buyer scoping and then uses the same public buyer accept workflow as the email link,
including idempotency handling, response activity, lien status updates, and buyer/seller outcome notifications.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Request Body:**

```json
{
  "notes": "Accepted from the funding portal."
}
```

`notes` is optional. Use an `Idempotency-Key` header for repeat-safe posts.

**Response:** `200 OK`, same JSON shape as `GET /api/liens/selling/public/{token}` with an accepted `accessLink`.

### POST `/api/liens/selling/buyer/liens/{accessLinkId}/decline`

Records a declined buyer response from the authenticated funding-company detail page using the same shared response
workflow as the public email link.

**Permission:** `SYNQ_LIENS.lien:browse` or the `SYNQLIEN_BUYER` product role when role fallback is enabled.

**Request Body:**

```json
{
  "reason": "Outside current buying criteria."
}
```

`reason` is optional. Use an `Idempotency-Key` header for repeat-safe posts.

**Response:** `200 OK`, same JSON shape as `GET /api/liens/selling/public/{token}` with a declined `accessLink`.

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
caller-provided CTA data. Seller name is resolved from the Identity user who confirmed/submitted the offer
(`SellingBuyerAccessLinks.CreatedByUserId` / confirm-sale acting user -> `idt_Users.FirstName` + `LastName`), scoped to
the seller organization when Identity validates membership;
seller company is resolved from the selling organization (`sellerOrgId`) with the same resolver used by the confirm-sale
email and authenticated funding-company views. Handling law firm is the selected standalone law-firm contact's
`liens_Contacts.Organization` value, falling back to its `DisplayName` when organization is absent. Law-firm and case-manager
contacts remain case/asset metadata and are not used as the buyer-facing seller identity. For buyer-purpose links, the `account` block indicates whether the access link has already
activated an account or whether the token-scoped buyer email already belongs to an Identity account, so the tenant portal
can render `Log In` instead of `Activate Free Account`.

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
    "name": "Seller Processor",
    "company": "RL Liens1",
    "email": null
  },
  "buyer": {
    "contactName": "Buyer contact",
    "company": "Funding company",
    "email": "buyer@company.test",
    "phone": "3105551212"
  },
  "case": {
    "handlingLawFirm": "Handling law firm",
    "handlingLawFirmContactName": "Law firm contact",
    "handlingLawFirmEmail": "lawfirm@example.test",
    "caseManager": "Case manager"
  },
  "documents": [
    {
      "id": "document-guid",
      "fileName": "real-document.pdf",
      "category": "Lien Document",
      "sizeOrType": "PDF",
      "viewUrl": "/api/lien/api/liens/selling/public/{token}/documents/{document-guid}/view",
      "downloadUrl": "/api/lien/api/liens/selling/public/{token}/documents/{document-guid}/download"
    }
  ],
  "messages": [
    {
      "id": "guid",
      "senderType": "buyer",
      "senderName": "Buyer contact",
      "senderEmail": "buyer@company.test",
      "message": "Can you confirm the signed LOP is final?",
      "createdAtUtc": "2026-07-23T14:05:00Z"
    }
  ],
  "account": {
    "hasExistingAccount": false,
    "loginUrl": "/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation&tenantId=offer-tenant-guid"
  }
}
```

The `account.loginUrl` includes the token-scoped offer tenant id so existing buyer accounts with access to multiple
SynqLien funding organizations sign into the tenant that issued the offer.

For seller-view links, `audience` is `seller`; the same JSON includes buyer/funding-company details. Seller-view links
can post messages, but response and activation endpoints reject that token with `403 read-only-link`. Seller-view JSON
does not include an account-action requirement; `account` may be `null`.

The `documents` array is limited to servicing document records attached to the offered lien. Selling v2 document
references (`SellingDocumentReference`) are read from their JSON metadata, while legacy lien document records still use
the existing semicolon metadata. Case-level documents that are not attached to the lien are excluded. `viewUrl` and
`downloadUrl` are same-origin tenant-portal BFF paths that preserve the public offer token and redirect through Liens to
the anonymous Documents access-token route.

### GET `/api/liens/selling/public/{token}/documents/{documentId}/view`

Issues a short-lived Documents view access token for a document attached to the token-scoped lien, then redirects to the
anonymous Documents access route. This endpoint is anonymous but requires the same valid, unexpired, unrevoked public
offer token as the portal `GET`. Buyer-response and seller-view tokens can both open lien documents. Documents not
attached to that lien return `404 document-not-found`.

**Authentication:** None.

**Response:** `302 Found`

`Location` points to `/documents/access/{accessToken}` when called through the gateway. The tenant portal BFF path
`/api/lien/api/liens/selling/public/{token}/documents/{documentId}/view` rewrites that redirect to
`/api/lien/documents/access/{accessToken}` for same-origin browser access. When local Documents storage then redirects
to `/internal/files`, the tenant portal keeps that final file hop under `/api/lien/documents/internal/files`.

### GET `/api/liens/selling/public/{token}/documents/{documentId}/download`

Same validation and ownership checks as the public document view endpoint, but requests a Documents download access
token.

**Authentication:** None.

**Response:** `302 Found`

### POST `/api/liens/selling/public/{token}/messages`

Adds a message to the token-scoped buyer/seller offer thread. This is anonymous and uses the same token validation as
the public `GET`. Liens derives the sender from the access-link purpose (`buyer` for buyer-response links, `seller` for
seller-view links); callers do not provide or override `senderType`. The message is persisted for the exact tenant,
lien, seller organization, buyer organization, and buyer contact represented by the token, so both public links see the
same chronological thread. After the message is saved, Liens emails the other party with that party's public link using
`lien.offer.message.created` and a message/recipient-specific idempotency key. Buyer-to-seller message notifications
use the seller account email resolved from Identity; seller-to-buyer replies use the activated or authenticated buyer
account email, not law-firm/contact email. Accept/decline outcome emails use the same account-recipient rule for the
seller, and the authenticated/activated buyer account email for the buyer when available. Notification failures are
logged and do not roll back the saved message or response.

**Authentication:** None.

**Request:**

```json
{
  "message": "Can you confirm the signed LOP is final?"
}
```

Messages must be 400 characters or fewer.

**Response:** `201 Created`

```json
{
  "id": "guid",
  "senderType": "buyer",
  "senderName": "Buyer contact",
  "senderEmail": "buyer@company.test",
  "message": "Can you confirm the signed LOP is final?",
  "createdAtUtc": "2026-07-23T14:05:00Z"
}
```

### POST `/api/liens/selling/public/{token}/activate-account`

Creates a buyer portal account for the token-scoped buyer organization. This endpoint is anonymous, uses the
same token validation as the public `GET`, and is intended to be called by the tenant portal BFF path
`/api/lien/api/liens/selling/public/{token}/activate-account`. Liens asks Identity to create or resolve a tenant-scoped
`LIEN_OWNER` organization for the source Liens buyer organization id, then Identity grants `SYNQ_LIENS` product access
and assigns `SYNQLIEN_BUYER` scoped to that Identity organization. Existing buyer contact values from the token win over
editable request values; request values only fill missing contact data. On successful activation, Liens records the
activated Identity user/email on the access link so later public `GET` requests continue to return
`account.hasExistingAccount=true` even when the original buyer contact did not have an email. Existing account emails
return `409` and should be handled by prompting the buyer to log in with the existing account.

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
  "loginUrl": "/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation&tenantId=offer-tenant-guid"
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
conflict behavior as public accept. Declining can record an optional reason, records the buyer access-link response as
`Declined`, and returns the seller lien to `Pending` so it appears in the seller Pending list and can be submitted for sale
again. It does not mark the lien sold, withdraw the seller listing, or create a Bill of Sale. Seller-view tokens are
read-only and return `403 read-only-link`. The first declined response submits
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
    "status": "Draft",
    "sellerStatus": "Pending"
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

### POST `/api/liens/cases/global-search`

Search cases, liens, and the legacy global-search categories for the authenticated tenant. The request accepts
`query` or the legacy alias `keyword`, plus optional `page` and `limit` values. The response preserves the paginated
`cases` and `liens` objects and adds the legacy `plaintiffs`, `lawFirms`, `medicalFacilities`, `medicalProviders`,
`fundingCompanies`, `Leads`, and `servicing` arrays. Funding-company results include both imported `LienHolder`
contacts and canonical `FundingCompany` contacts.

**Permission:** `SYNQ_LIENS.case:read`

---

### GET `/api/liens/cases`

Search and list cases with optional filters.

Case statuses include `PreDemand`, `DemandSent`, `InNegotiation`, `Litigation (Open)`,
`Litigation (Pending)`, `CaseSettled`, and `Closed`. The two litigation variants are
stored values and can be filtered independently.

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

**Response:** `200 OK` — `CaseResponse`. The response includes the latest linked lien's UI lifecycle
label in `lienStatus` and matching LienStatus lookup UUID in `lienStatusId`. It also includes the latest
settlement payment's display value in `settlementStatus` and its stored lookup ID or code in
`settlementStatusId` only when the case has at least one lien and every linked lien is `Settled`
(legacy/UI `Closed`), or when any settlement or payment record on the case declares `No Recovery`. A No
Recovery declaration remains visible while other liens are open and is normalized to `No Recovery` with
legacy settlement-status ID `4`. Other settlement statuses remain empty while any linked lien is open or
rejected; cases without liens also return empty settlement fields. Each field pair also returns empty
strings when its corresponding record does not exist.

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
Potential duplicate cases are rejected before save when DOB and date of loss exactly match an existing case and first/last names closely or partially match. Clients can call `POST /api/liens/cases/duplicate-check` before create to display the existing case link.

### POST `/api/liens/cases/duplicate-check`

Checks a pending case creation for duplicate risk without saving.

**Permission:** `SYNQ_LIENS.case:create`

**Request Body:**

```json
{
  "firstname": "Jane",
  "lastname": "Doe",
  "dob": "01/15/1990",
  "dateOfLoss": "08/01/2026"
}
```

**Response:** `200 OK`

```json
{
  "isDuplicate": true,
  "message": "A case with similar information already exists. Would you like to view the existing case?",
  "matches": [
    {
      "id": "guid",
      "caseNumber": "26-00042",
      "clientDisplayName": "Jane Doe",
      "clientDob": "1990-01-15",
      "dateOfIncident": "2026-08-01",
      "status": "PreDemand"
    }
  ]
}
```

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
| `status` | `string` | No | Yes | Case status. Accepted values include `Litigation (Open)` and `Litigation (Pending)`. |
| `demandAmount` | `decimal` | No | Yes | Demand amount |
| `settlementAmount` | `decimal` | No | Yes | Settlement amount |

**Response:** `200 OK` — `CaseResponse`

**Error:** `404 Not Found` — if the case does not exist.

---

### GET `/api/liens/cases/notes/{caseId}`

Return the legacy case-note history. Each changed non-empty `notes` value submitted through `PATCH /api/liens/cases/details-update` is appended as a new case-note entry rather than replacing prior entries. Feed notes and system update-history entries are intentionally excluded.

Every explicit `notes` change through `details-update`, including clearing an existing value, also creates a case-update history entry with action `Case Details Update` and description `Case Tracking Note Update`. Repeating the same normalized Notes value does not create a duplicate update. When Notes changes together with other case-detail fields, `Case Tracking Note Update` is included in the existing combined change description returned by `POST /api/liens/cases/case-updates/v3`. Case creation history prefers the authenticated user's email over the token's organization-oriented display name for its `updatedBy` value.

**Permission:** `SYNQ_LIENS.case:read`

The response uses the legacy envelope `{ isSuccess, message, data }`. `data` is ordered newest first and each item includes the historical `note` value and creator metadata. `created` is the U.S. Pacific display string, while `createdAtUtc` is the corresponding canonical UTC ISO timestamp.

`POST /api/liens/cases/add-note` and `POST /api/liens/cases/get-notes` are the separate Feed-note routes. Feed notes are shown only in the case Feed; they are not returned by this case-notes endpoint or by case-update history.

---

### POST `/api/liens/cases/dashboard/deployed` and `/api/liens/cases/dashboard/cash-received`

Return dashboard totals for deployed liens and cash received. Supplying both `startDate` and `endDate` filters the metric to that inclusive range. When neither date is supplied, the metric includes all dated tenant history; `periodStart` and `periodEnd` are returned as empty strings to indicate the all-time result. Deployed always excludes liens without a persisted `PurchaseDate`, and Cash Received always excludes settlement headers without a persisted `SettlementDate`.

The dashboard Total Lien Report, including its status chart and totals, excludes `Rejected` and `Cancelled` liens before aggregation and pagination.

---

### GET `/api/liens/cases/payoff-quote/{caseId}`

Compatibility alias: `GET /api/liens/cases/payoff-qoute/{caseId}`.

Returns the latest payoff statement URL for the case. If no payoff document exists, the service generates a payoff PDF from the case and its open servicing liens, uploads it to the Documents service as a case document, records `LegacyCaseDocument` metadata with legacy type ID `14`, and returns the uploaded document URL.

**Response:** `200 OK`

```json
{
  "isSuccess": true,
  "message": "Successfully retrieved Payoff Quote",
  "url": "/documents/{documentId}",
  "base64": "JVBERi0xLjQ..."
}
```

Missing cases return `404` with `Error: Unable to retrieve Payoff Quote`.

---

## Upload Limits

All SynqLien multipart upload endpoints accept files up to 50 MB. Requests over the limit return a size error instead of a generic upload failure.

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

### Legacy document retrieval and opening

`GET /api/liens/cases/get-casedocument/{caseId}`, `GET
/api/liens/cases/liens/get-medicaldocument/{liensId}`, and `GET
/api/liens/cases/get-allcasedocument/{caseId}` return a legacy `url` field.
Document responses also return `documentTypeId`, normalized to the UUID used by
`GET /lookup/document/type`. The existing `typeId` remains available for legacy
callers. When historical metadata has no usable type, both fields fall back to
the canonical `Other` document type so the tenant portal always displays a label.

Current uploads return `/documents/{documentId}` and must be opened through the
Documents-service view-token endpoint. SQL-migrated SL-CORE records instead
retain an allowlisted `https://legal-dmm-prod.legalsynq.com/...` URL because
they do not have a Documents-service ID. The tenant portal's BFF resolves the
legacy object key through a tenant-scoped Liens endpoint and redirects only to
that exact HTTPS host; browser code continues using the existing view-token flow.

### GET `/api/liens/legacy-document-links/{objectKey}/resolve`

Protected compatibility endpoint used by the tenant portal BFF when an existing
Documents-service `view-url` request contains a migrated legacy object key
instead of a Documents GUID. It is tenant-scoped, accepts only a safe filename
key, and returns a URL only when exactly one `LegacyCaseDocument`,
`LegacyLienDocument`, or `LegacyMedicalDocument` record resolves to the
allowlisted legacy host.

**Permission:** `SYNQ_LIENS.case:read`

**Response:** `200 OK`

```json
{
  "url": "https://legal-dmm-prod.legalsynq.com/path/to/document.pdf"
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

### POST `/api/liens/contacts/export-csv`

Export all matching active, top-level contacts as a Base64-encoded CSV. The default columns match the Contacts table: the selected contact-type name, Email, and Active Cases.

**Permission:** `SYNQ_LIENS.lien:service`

**Request Body:**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `contactType` | `string` | No | `null` | Contact-type tab to export |
| `search` | `string` | No | `null` | Matches the Contacts table search |
| `legacyFormat` | `boolean` | No | `false` | Returns the previous ten-column schema, including inactive and sub-contact records |

**Response:** `200 OK` — `{ "data": "<base64 CSV>" }`

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

## Settlement Reduction Endpoints

Base path: `/api/liens/settlement/reductions`

`GET /case/{caseId}` and `GET /lien/{lienId}` return canonical lien reductions.
For a lien without a canonical reduction, the response also exposes preserved
SL-CORE settlement metadata containing both a valid `reductionAmount` and an
explicit `SLS_REDUCTION_DATE`. Historical source rows without a reduction date
are omitted from this compatibility fallback; the service does not invent a
date. A canonical reduction takes precedence over the legacy fallback for the
same lien.

---

## Settlement Payment Endpoints

Base path: `/api/liens/settlement/payments`

### POST `/service/update-liens-status`

Legacy servicing endpoint for closing one or more selected liens and declaring No Recovery. `caseId`,
comma-delimited `lienIds`, `lienStatus`, and `closedDate` are required; `closedDate` accepts `yyyy-MM-dd`
and US `MM/dd/yyyy` formats. Every selected lien must belong to the authenticated tenant and the supplied
case. The update is atomic on relational databases: each selected lien receives `lienStatus`, and a
zero-amount payment-detail declaration is recorded for `closedDate` with the optional `note` and canonical
No Recovery settlement status ID `4`.

The No Recovery declaration is case-level for display compatibility. A subsequent
`GET /api/liens/cases/{id}` returns `settlementStatus: "No Recovery"` and `settlementStatusId: "4"` even
when other liens on the case remain open.

### PUT `/api/liens/settlement/payments/{paymentId}`

Update one existing settlement payment. The payment is resolved from the authenticated tenant and the route `paymentId`; `caseId` and `lienId` are immutable and are not accepted in the body. The legacy `POST /service/liens/update/settlement` remains a create-settlement endpoint and must not be used to edit a payment.

**Permission:** `SYNQ_LIENS.lien:update`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `paymentId` | `guid` | Settlement payment identifier returned by payment-details APIs |

**Request Body: `UpdateSettlementPaymentDetailRequest`**

All fields are required. Unknown JSON fields are rejected with `400 Bad Request`.

| Field | Type | Description |
|---|---|---|
| `amount` | `decimal` | Updated payment amount; must be zero or greater |
| `paymentDate` | `date` | Payment date in `YYYY-MM-DD` format |
| `paymentMethod` | `string` | Nonblank payment method, such as `Check` |
| `referenceNumber` | `string` | Nonblank check or external payment reference number; maximum 100 characters |
| `notes` | `string` | User-visible payment note; must be present and non-null but may be empty |
| `settlementType` | `string` | Nonblank settlement source, such as `by_funding_company` |
| `settlementStatus` | `string` | Nonblank payment outcome, such as `full_payment` |
| `lienStatus` | `string` | Linked lien lifecycle value. `Open` and `Closed` normalize to `Active` and `Settled` |

```json
{
  "amount": 530,
  "paymentDate": "2026-08-16",
  "paymentMethod": "Check",
  "referenceNumber": "123456",
  "notes": "Payment Testing",
  "settlementType": "by_funding_company",
  "settlementStatus": "full_payment",
  "lienStatus": "Closed"
}
```

**Response:** `200 OK` — `SettlementPaymentDetailResponse`

The response returns the immutable payment identity and linkage, the updated amount/date/reference/note, `paymentMethod`, `settlementTypeId`, `settlementStatusId`, and audit fields. Settlement classification remains stored in the existing payment metadata representation; unrelated metadata is preserved. The payment update and linked-lien status change commit atomically.

Because payment method and settlement classifications use the legacy metadata representation, `paymentMethod`, `settlementType`, and `settlementStatus` reject `;`, `=`, CR/LF, and the `[legacy-meta]` marker. `notes` rejects the exact `[legacy-meta]` marker but otherwise permits normal punctuation, including semicolons and equals signs.

**Errors:**

| Status | Condition |
|---|---|
| `400 Bad Request` | Missing, malformed, unknown, or invalid request field |
| `404 Not Found` | Payment is missing, deleted, or belongs to another tenant |

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

### POST `/api/liens/reports/case-notes-history`

Returns the tenant-wide Case Notes History report used by the Case Tracking Notes and Feed Notes tabs. The compatibility alias is `POST /report/case-notes-history`. Both routes require authenticated SynqLien product access and `SYNQ_LIENS.case:read`, use the tenant from the authenticated request context, and return `Cache-Control: no-store`.

Request:

```json
{
  "noteType": "TRACKING",
  "page": 1,
  "limit": 10,
  "sortBy": "noteDate",
  "sortDirection": "desc"
}
```

`noteType` is required and accepts `TRACKING` or `FEED` case-insensitively. `TRACKING` includes `general` and `follow-up` notes; `FEED` includes only `feed`. Deleted, blank, internal, case-created, and settlement-history notes are excluded. `page` defaults to 1, `limit` defaults to 10 and is limited to 1-100. `sortBy` accepts `caseId`, `caseName`, `noteType`, `noteDate`, `noteAuthor`, or `noteContent`; every order is stabilized by note timestamp and ID.

```json
{
  "isSuccess": true,
  "message": "Case notes history retrieved.",
  "data": [
    {
      "noteId": "019f0000-0000-7000-8000-000000000001",
      "caseRecordId": "019f0000-0000-7000-8000-000000000002",
      "caseId": "26-31959",
      "caseName": "Greenfield Holdings",
      "noteType": "TRACKING",
      "noteTypeLabel": "Case Tracking Note",
      "noteDate": "2026-07-28",
      "createdAtUtc": "2026-07-28T16:30:00.0000000Z",
      "noteAuthor": "Sarah Mitchell",
      "noteContent": "Complete full note text"
    }
  ],
  "page": 1,
  "limit": 10,
  "totalCount": 37,
  "isComplete": false,
  "excludedUnreconciledLegacyNoteCount": 2,
  "warning": {
    "code": "legacy_history_incomplete",
    "message": "Some unreconciled legacy case notes were excluded. Native and reconciled notes are included.",
    "excludedCount": 2
  }
}
```

The legacy alias omits only the additive `createdAtUtc` row property. An empty or out-of-range page is `200` with an empty `data` array. Invalid selectors, paging, or sort values return `400` with `error.code = validation_error`. Native notes and reconciled legacy notes remain visible when stale legacy crosswalks exist. Only eligible report notes whose target IDs belong to unreconciled, tenant-matching `SL-CORE` `SL_CASE_NOTES` crosswalks are excluded. Both routes return `isComplete`, `excludedUnreconciledLegacyNoteCount`, and a nullable `warning`; complete responses set the count to `0`, `isComplete=true`, and `warning=null`.

### POST `/api/liens/reports/case-notes-history/export`

Exports all rows matching `noteType` and the requested ordering; `page` and `limit` are ignored. The compatibility alias is `POST /report/case-notes-history/export`. The CSV contains the six visible report columns, preserves complete Unicode/multiline content, quotes CSV fields, and neutralizes spreadsheet-formula prefixes. The Base64 CSV envelope is retained for legacy clients:

```json
{
  "isSuccess": true,
  "message": "CSV generated successfully.",
  "isComplete": false,
  "excludedUnreconciledLegacyNoteCount": 2,
  "warning": {
    "code": "legacy_history_incomplete",
    "message": "Some unreconciled legacy case notes were excluded. Native and reconciled notes are included.",
    "excludedCount": 2
  },
  "data": [
    {
      "base64": "Q2FzZSBJRCxDYXNlIE5hbWUuLi4=",
      "filename": "case_notes_history_tracking_20260813123000.csv",
      "export_format": "csv"
    }
  ]
}
```

CSV generation stops at 10 MiB and returns `400 validation_error` rather than materializing an unbounded export. Export uses the same tenant-scoped unreconciled-target exclusion and additive completeness fields as preview, so eligible native and reconciled rows are exported without silently including stale legacy classifications.

### POST `/api/liens/reports/auto-generated/{reportId}/execute`

Executes the tenant-scoped stored report using its saved report date. The compatibility alias is `POST /report/auto-generated/{reportId}/execute`. No request body is required.

Query parameters:

- `page`: optional, defaults to `1`, and must be at least `1`.
- `pageSize`: optional, defaults to `50`, and must be between `1` and `100`.

The stored report date ends the inclusive seven-day purchase range (`date - 6 days` through `date`). Eligible Weekly BCC liens are ordered by purchase date, lien number, and record ID before database paging. Only the selected page is enriched. An out-of-range page returns `200` with an empty `data` array while retaining the full-result count and column schema.

```json
{
  "isSuccess": true,
  "message": "Weekly BCC report generated.",
  "report": {
    "reportId": 42,
    "code": "weekly_bcc_2026-08-14",
    "description": "Weekly BCC - 08/14/2026",
    "date": "2026-08-14",
    "createDate": "2026-08-14T11:00:00Z",
    "tenantId": "11111111-1111-1111-1111-111111111111",
    "apiPath": "/api/liens/reports/weekly-bcc"
  },
  "reportType": "WEEKLY_BCC",
  "schemaVersion": 1,
  "asOfDate": "2026-08-14",
  "page": 1,
  "pageSize": 50,
  "totalPages": 4,
  "totalCount": 187,
  "summaryTotals": {
    "totalCases": 120,
    "totalOpenCases": 80,
    "totalClosedCases": 40,
    "totalLiens": 187,
    "totalOpenLiens": 130,
    "totalClosedLiens": 57,
    "totalPurchaseAmt": 22462370.62,
    "totalReturnedAmt": 19089906.53,
    "totalBillingAmt": 79778606.30
  },
  "columns": [
    { "key": "plaintiffFirstName", "label": "Plaintiff First Name", "index": 0 },
    { "key": "caseId", "label": "Case ID", "index": 9 }
  ],
  "data": [
    {
      "plaintiffFirstName": "Ada",
      "caseId": "CASE-001"
    }
  ]
}
```

`columns` always contains all 57 Weekly BCC v1 descriptors. Keys use the same camelCase names as the objects in `data`, and indexes are unique, contiguous, and zero-based (`0` through `56`). The `noted` field is labeled `Notes` in report previews and CSV exports. Invalid pagination returns `400`; missing or cross-tenant reports return `404`; unsupported stored report paths return `409`. Both direct and saved-report execution responses add `summaryTotals` with `totalCases`, `totalOpenCases`, `totalClosedCases`, `totalLiens`, `totalOpenLiens`, `totalClosedLiens`, `totalPurchaseAmt`, `totalReturnedAmt`, and `totalBillingAmt` calculated from the complete eligible result set.

For the `reduction` field, canonical lien reductions take precedence when any exist for the lien. Preserved SL-CORE settlement metadata containing `reductionAmount` is used only when the lien has no canonical reduction.

### POST `/api/liens/reports/auto-generated/{reportId}/export`

Exports all eligible rows from the tenant-scoped stored Weekly BCC report. The compatibility alias is `POST /report/auto-generated/{reportId}/export`. No request body or pagination parameters are required.

Rows retain the same deterministic purchase-date, lien-number, and record-ID order as execution. The exporter enriches bounded pages into a delete-on-close temporary file, writes headers in the versioned 57-column order, quotes CSV values, preserves Unicode and multiline content, and neutralizes spreadsheet-formula prefixes. After size validation, the API Base64-encodes that file incrementally into the response without buffering the full CSV or Base64 string in memory. The response uses the established Base64 CSV envelope:

```json
{
  "isSuccess": true,
  "message": "CSV generated successfully.",
  "data": [
    {
      "base64": "UGxhaW50aWZmIEZpcnN0IE5hbWUsLi4u",
      "filename": "weekly_bcc_20260814.csv",
      "export_format": "csv"
    }
  ]
}
```

The configurable raw CSV limit is enforced before Base64 encoding. `AutoGeneratedReports:ExportSizeLimitMiB` defaults to 50 MiB and accepts values from 1 through 100 MiB. `AutoGeneratedReports:MaximumConcurrentExports` defaults to 2 and accepts values from 1 through 10; the process-wide lease is held through response streaming, and saturated requests return `429` with `Retry-After: 5` and `error.code = too_many_requests`. An oversized export returns `400` with `error.code = validation_error` and identifies the configured ceiling; missing or cross-tenant reports return `404`; unsupported stored report paths return `409`.

### GET `/report/diy/columns`

Returns the legacy DIY-report column metadata and the ordered default selection for the requested report type. For `LIENS`, the default selection includes `days_since_reduction_approval` in position 9 (zero-based), followed by `case_status` and `date_of_loss` in positions 14 and 15 respectively. `initial_service_date` and `number_of_liens` remain available as optional columns but are not selected by default.

DIY lien reports use canonical lien reductions before preserved SL-CORE settlement metadata. When a legacy reduction has no explicit source reduction date, `reduction_date` and `days_since_reduction_approval` are null; a settlement date is never substituted for a reduction approval date.

The optional `notes` column returns the latest active, nonblank Feed note for the row's case. `notes_date` returns that exact note's creation date in `MM/dd/yyyy` format. Both columns are grouped under `procedureInfo`, are not selected by default, and use one tenant-scoped batch lookup for CASES, LIENS, and COMBINED reports. Deleted, blank, non-Feed, and cross-tenant notes are excluded. When no eligible Feed note exists, `notes` is empty and `notes_date` is null in preview responses and blank in CSV exports. Equal creation timestamps are resolved by descending note ID. Saved report preview and export use the same mapping.

The existing compatibility keys have these Tracking Notes definitions:

| Key | Label | Value |
|---|---|---|
| `last_case_note` | `Tracking Notes` | All active, nonblank General and Follow-up notes for the case, newest first and separated by `\n` line breaks |
| `last_case_note_date` | `Last Tracking Note Date` | Date of the newest included Tracking Note in `MM/dd/yyyy` format |

Feed, internal, system/history, deleted, blank, and cross-tenant notes are excluded. `POST /report/diy` and its canonical `/api/liens/reports/diy/run` route return the same aggregated value. Both DIY export routes quote the multiline field in the Base64-encoded CSV, so every Tracking Note is retained.

The distinct Case Update fields use the newest active Case Activity row for the tenant. `last_case_tracking_note` is exposed as `Last Activity` and contains its normalized Description; `last_case_tracking_date` is exposed as `Last Activity Date` and contains its Pacific-time Timestamp in `MM/dd/yyyy hh:mm tt` format. Eligible rows match the Case Activity table (`Case Created` and internal Case Details Update entries), equal timestamps use descending activity ID, and preview and CSV export use the same mapping.
