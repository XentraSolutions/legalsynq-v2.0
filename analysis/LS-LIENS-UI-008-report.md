# LS-LIENS-UI-008: Audit Timeline & Activity Stream

## Status: COMPLETE

## Summary
Built a 5-file audit service layer and a reusable `EntityTimeline` component, then wired it into all 5 lien entity detail pages (Cases, Liens, Servicing, BillOfSale, Contacts).

## Backend Analysis
- **Audit Service**: Separate microservice at `apps/services/audit/`
- **Key Endpoint**: `GET /audit/entity/{entityType}/{entityId}` returns `ApiResponse<AuditEventQueryResponse>`
- **Gateway Routing**: `/audit-service/audit/{**catch-all}` strips prefix `/audit-service` and forwards to audit cluster
- **Frontend Path**: `/api/audit-service/audit/entity/{type}/{id}` via Next.js fallback rewrite to gateway
- **DTO Shape**: `AuditEventQueryResponse` wraps paginated `AuditEventRecordResponse[]` with `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`, `HasNext`, `HasPrev`, time-range metadata
- **Record Shape**: `AuditEventRecordResponse` includes `AuditId`, `EventType`, `Action`, `Description`, `Actor` (nested), `Entity` (nested), `OccurredAtUtc`, `Severity`, `Tags[]`, `Before`/`After` JSON diffs, correlation IDs

## Files Created

### Service Layer (`apps/web/src/lib/audit/`)
| File | Purpose |
|------|---------|
| `audit.types.ts` | TypeScript interfaces mirroring backend DTOs + mapped timeline types |
| `audit.api.ts` | API client calls via `apiClient.get()` for entity events and general events |
| `audit.mapper.ts` | Maps `AuditEventRecordDto` to `TimelineItem`, formats timestamps and actions |
| `audit.service.ts` | High-level service with typed methods per entity (Case, Lien, ServicingItem, BillOfSale, Contact, Document) |
| `index.ts` | Barrel export |

### Component
| File | Purpose |
|------|---------|
| `apps/web/src/components/lien/entity-timeline.tsx` | Reusable component: takes `entityType` + `entityId`, fetches audit events, renders timeline with loading/error/empty states and pagination |

## Files Modified

### Detail Pages with EntityTimeline Integration
| File | Entity Type |
|------|-------------|
| `apps/web/src/app/(platform)/lien/cases/[id]/page.tsx` | `Case` |
| `apps/web/src/app/(platform)/lien/liens/[id]/page.tsx` | `Lien` |
| `apps/web/src/app/(platform)/lien/servicing/[id]/page.tsx` | `ServicingItem` |
| `apps/web/src/app/(platform)/lien/bill-of-sales/[id]/page.tsx` | `BillOfSale` |
| `apps/web/src/app/(platform)/lien/contacts/[id]/page.tsx` | `Contact` |

## Architecture Decisions
1. **Gateway fallback rewrite**: No BFF route needed; Next.js `next.config.mjs` fallback (`/api/:path*` to gateway) handles routing to the audit service automatically
2. **Graceful 404 handling**: If audit service returns 404 (no events yet), component shows "No activity yet" instead of error
3. **Pagination built-in**: Component supports multi-page navigation for entities with many audit events
4. **Entity types match backend**: Uses exact casing from backend: `Case`, `Lien`, `ServicingItem`, `BillOfSale`, `Contact`
5. **Existing `activity-timeline.tsx` preserved**: The old mock-data-driven component is left intact; the new `entity-timeline.tsx` is the API-driven replacement used in detail pages

## Patterns Followed
- 5-file service layer pattern (types, api, mapper, service, barrel)
- `apiClient` from `@/lib/api-client` for HTTP calls
- `ApiError` catch for graceful error handling
- Consistent card styling matching existing detail page sections
