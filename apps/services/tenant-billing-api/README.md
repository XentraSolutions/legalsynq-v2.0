# Tenant Billing API

Multi-tenant billing service exposing customers, invoices, and payments under
`/api/customers`, `/api/invoices`, and `/api/payments`.

## Run locally

```bash
cd services/tenant-billing-api
dotnet run --project src/TenantBilling.Api
```

The service listens on `ASPNETCORE_URLS` (defaults to `http://localhost:5001`).
Swagger UI is available at `/swagger` in the Development environment.

## Tenant resolution (TBS-B02)

Every request to `/api/*` must carry the active tenant on the
`X-Tenant-Id` request header. The value must be a non-empty GUID.

```http
GET /api/customers HTTP/1.1
X-Tenant-Id: 11111111-1111-1111-1111-111111111111
```

### Why a header

The platform's identity story has not yet landed (no JWT, no OIDC, no API
gateway claim transform). A header keeps the contract explicit and easy to
swap later — when claims arrive, only `HttpHeaderTenantContext` and
`TenantResolutionMiddleware` change; controllers and repositories already
consume the value through the `ITenantContext` abstraction.

### Enforcement

- `TenantResolutionMiddleware` parses the header for any path under `/api/`
  and short-circuits with **HTTP 400** when it is missing, malformed, or
  `Guid.Empty`. No controller code runs without a valid tenant.
- `ITenantContext` (`HttpHeaderTenantContext`) exposes the parsed tenant id
  to controllers and throws if read outside a resolved request.
- All read methods on `ICustomerRepository` / `IInvoiceRepository` /
  `IPaymentRepository` are tenant-scoped (`GetByIdForTenantAsync`,
  `GetAllForTenantAsync`). There are no unscoped equivalents, so a future
  caller cannot accidentally bypass the filter.
- Cross-tenant `GET /{id}` returns **HTTP 404** (no existence leak).
- Cross-tenant `GET /` (list) returns an empty array.
- Cross-tenant write attempts that reference another tenant's customer or
  invoice surface as **HTTP 400** with a generic "not found" message — the
  same response the caller would get for a truly missing id.
- The `TenantId` field has been removed from `POST` request bodies; the
  header is the single source of truth so a request cannot disagree with
  itself about which tenant it targets.

### Endpoints

| Verb   | Route                          | Tenant header required |
|--------|--------------------------------|------------------------|
| GET    | `/health`                      | no                     |
| GET    | `/api/customers`               | yes                    |
| GET    | `/api/customers/{id}`          | yes                    |
| POST   | `/api/customers`               | yes                    |
| GET    | `/api/invoices`                | yes                    |
| GET    | `/api/invoices/{id}`           | yes                    |
| POST   | `/api/invoices`                | yes                    |
| GET    | `/api/payments`                | yes                    |
| GET    | `/api/payments/{id}`           | yes                    |
| POST   | `/api/payments`                | yes                    |

### Quick check

```bash
curl -i http://localhost:5001/api/customers
# HTTP/1.1 400 Bad Request — Missing required 'X-Tenant-Id' header.

curl -i -H 'X-Tenant-Id: 11111111-1111-1111-1111-111111111111' \
  http://localhost:5001/api/customers
# HTTP/1.1 200 OK — []
```
