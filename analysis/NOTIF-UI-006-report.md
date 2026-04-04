# NOTIF-UI-006 Implementation Report

## 1. Implementation Summary

### What was built
NOTIF-UI-006 adds the first tenant-facing self-service notification feature: Tenant Branding Self-Service in the Tenant Portal (apps/web). This allows tenant admins and operations users to manage their organisation's notification branding directly, without requiring platform admin assistance.

### Scope completed vs requested scope
All 11 acceptance criteria are fully implemented:
1. Tenant branding page exists in the tenant portal ✅
2. Tenant branding list works ✅
3. Tenant branding create works ✅
4. Tenant branding edit works ✅
5. Tenant users never enter tenantId manually ✅
6. Tenant context is handled correctly and safely ✅
7. Only tenant-owned branding records are shown/editable ✅
8. Loading / success / error states are handled ✅
9. Empty states are handled clearly ✅
10. No Control Center/admin-only capabilities are exposed ✅
11. UI compiles/runs cleanly inside the main platform ✅

### Overall completeness assessment
Complete. All requested features are implemented. No overbuilding.

## 2. Files Created / Modified

### New files
| File | Purpose |
|------|---------|
| `apps/web/src/app/(platform)/notifications/branding/page.tsx` | Tenant branding list page (server component) |
| `apps/web/src/app/(platform)/notifications/branding/actions.ts` | Server actions for branding create/update |
| `apps/web/src/app/(platform)/notifications/branding/branding-list-client.tsx` | Client component with list/create/edit/preview states |
| `apps/web/src/components/notifications/tenant-branding-form.tsx` | Shared branding form (create + edit) with live preview |
| `apps/web/src/components/notifications/branding-preview-card.tsx` | Visual preview card showing brand appearance |
| `apps/web/src/components/notifications/branding-empty-state.tsx` | Empty state with explanation and CTA |
| `apps/web/src/components/notifications/product-type-badge.tsx` | Reusable product type badge component |
| `apps/web/src/components/notifications/color-swatch-field.tsx` | Colour picker + text input field component |
| `apps/web/src/lib/notifications-shared.ts` | Client-safe shared types and constants (ProductType, TenantBranding, etc.) |

### Modified files
| File | Changes |
|------|---------|
| `apps/web/src/lib/notifications-server-api.ts` | Added branding types (`TenantBranding`, `ProductType`, `BrandingListResponse`), extended `notifRequest` with mutation support (`method`, `body`), added branding CRUD methods, improved error parsing for nested `{ error: { message, details } }` |
| `apps/web/src/app/(platform)/notifications/page.tsx` | Added "Branding" link button in the notifications overview header |

## 3. Features Implemented

### Branding list
- Route: `/notifications/branding`
- Shows all branding records belonging to the authenticated tenant
- Product type filter pills (All, CareConnect, SynqLien, SynqFund, SynqRx, SynqPayout)
- Table with brand name (clickable to detail), product type badge, colour swatches, support email, updated date
- View and Edit actions per row
- Strong empty state with explanation of why branding matters and CTA to create first profile

### Create flow
- Full form with all branding fields
- Product type selector only shows products without existing branding
- Warning when all products already have branding profiles
- Required field validation (brand name, product type)
- Email format validation
- URL format validation (logo URL, website URL)
- Backend duplicate conflict error surfaced clearly
- Live preview updates as fields change
- Success state with auto-redirect
- Tenant ID injected automatically from session (never shown to user)

### Edit flow
- Pre-populated form with existing values
- Product type shown as read-only (not editable)
- Same validation rules as create
- Same live preview
- Success state with auto-redirect

### Preview/detail UX
- Detail view accessible by clicking brand name in list
- Shows all branding fields in a clean definition list layout
- Visual preview card with:
  - Brand name and logo in header bar (primary colour)
  - Sample notification body text
  - Sample button using accent colour and button radius
  - Contact information footer (secondary colour)
- "Edit Branding" action from detail view
- Explanation text about how preview relates to actual notifications

### Tenant-safe auth/context handling
- `requireOrg()` guard on page load (redirects to login if no session, /no-org if no org)
- Server actions use `requireOrg()` before any mutation
- `session.tenantId` injected as `x-tenant-id` header automatically
- No tenant ID input field anywhere in the UI
- Backend enforces tenant ownership on all branding operations

## 4. API / Backend Integration

### Branding reads
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /notifications/v1/branding` | List tenant branding records | Working |
| `GET /notifications/v1/branding/:id` | Get single branding record | Working |

### Branding mutations
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `POST /notifications/v1/branding` | Create branding for tenant+product | Working |
| `PATCH /notifications/v1/branding/:id` | Update existing branding | Working |

## 5. Data Flow / Architecture

### Request flow
1. Server component (`page.tsx`) calls `requireOrg()` to get session with `tenantId`
2. Calls `notificationsServerApi.brandingList(tenantId)` which sends `GET /notifications/v1/branding` with `x-tenant-id` header
3. Response parsed and passed to client component as props

### Tenant context flow
1. `requireOrg()` validates `platform_session` cookie via Identity service `/auth/me`
2. Returns `PlatformSession` with `tenantId` from JWT claims
3. `tenantId` passed to `notifRequest()` which injects `x-tenant-id` header
4. Backend uses `req.tenantId` (from `x-tenant-id` header) for all queries

### Server action / mutation flow
1. Client component calls server action via `useTransition`
2. Server action calls `requireOrg()` to get `tenantId`
3. Action calls `notificationsServerApi.brandingCreate/Update(tenantId, body)`
4. `notifRequest` sends POST/PATCH with `x-tenant-id` header + `Authorization: Bearer`
5. Returns `ActionResult` to client (success + data, or error message)

### Cache/revalidation flow
- All reads use `cache: 'no-store'` (no ISR caching)
- After mutations, `router.refresh()` triggers server component re-render
- Fresh data fetched on every page load

### Tenant-side API client design
- Extended existing `notificationsServerApi` in `notifications-server-api.ts`
- Added mutation support to `notifRequest()` with `method` and `body` options
- All methods require `tenantId` as first parameter — never optional
- Improved error parsing to handle nested `{ error: { message, details } }` response shape
- No separate client created — extended the existing pattern for consistency

## 6. Validation & Testing

### Typecheck/compile status
- PASS — All new/modified files compile cleanly
- Pre-existing errors in other parts of the web app remain unchanged

### Runtime checks
- PASS — App starts and serves pages correctly after restart

### Manual test flows
| Flow | Status |
|------|--------|
| Navigate to `/notifications/branding` | PASS |
| Product type filter pills | PASS |
| Empty state with CTA | PASS |
| Create branding form with all fields | PASS |
| Live preview updates on field change | PASS |
| Required field validation | PASS |
| Email format validation | PASS |
| URL format validation | PASS |
| Edit branding pre-populated values | PASS |
| Detail/preview view from list | PASS |
| Edit from detail view | PASS |
| Success state after create/update | PASS |
| Branding link from notifications overview | PASS |

### Edge cases handled
| Edge Case | Status |
|-----------|--------|
| No branding records | PASS — shows strong empty state |
| All products already have branding | PASS — shows warning, disables submit |
| Backend conflict (duplicate product) | PASS — error message displayed |
| Invalid email/URL/colour | PASS — client-side validation |
| Backend service unavailable | PASS — error message displayed |
| Missing tenant context | PASS — requireOrg() redirects to login |
| Advanced fields hidden by default | PASS — collapsible section |

## 7. Error Handling

### Form validation
- Required: brandName (all modes), productType (create)
- Email format check on supportEmail
- URL format check on logoUrl, websiteUrl
- Duplicate product check against existing records (client-side)

### Backend error handling
- Nested `{ error: { message, details } }` parsed and displayed
- 409 conflict errors surfaced clearly
- 400 validation errors shown with detail list
- Network errors caught and displayed generically

### Duplicate conflict handling
- Client-side: product type selector only shows products without existing branding
- Backend: 409 conflict error message surfaced if client-side check bypassed

### Missing tenant-context handling
- `requireOrg()` redirects to /login if no session
- `requireOrg()` redirects to /no-org if session has no org
- Server actions also call `requireOrg()` — unauthenticated mutations fail safely

### Empty states
- Main list: full empty state component with explanation, icon, and CTA
- Product filter with no results: same empty state
- Backend fetch error: red banner with error message

## 8. Tenant / Auth Context

### How tenant context is derived
- `requireOrg()` calls `getServerSession()` which reads `platform_session` cookie
- Cookie validated via Identity service `/auth/me` endpoint
- Returns `PlatformSession` containing `tenantId` from JWT claims

### How auth is enforced
- Page-level: `requireOrg()` in server component (redirects if unauthenticated)
- Action-level: `requireOrg()` in every server action (returns error if unauthenticated)
- Backend: branding controller checks `req.tenantId` for ownership on all operations

### How x-tenant-id is injected
- `notifRequest()` adds `x-tenant-id: ${tenantId}` to every request
- `tenantId` comes from `session.tenantId` — never from user input
- Both reads and mutations use the same mechanism

### Behavior when tenant context is missing
- No session: redirect to /login
- Session without org: redirect to /no-org
- Server action without session: error returned (not thrown)

## 9. Cache / Performance

### Cache tags invalidated
- No cache tags used — all reads are `cache: 'no-store'`

### Revalidation approach
- After mutations: `router.refresh()` triggers server component re-render
- Server component fetches fresh data on every request
- No ISR/stale data risk

### Performance considerations
- Branding list limited to 100 records per request (sufficient for tenant-scoped data)
- No pagination UI yet (tenant unlikely to have >100 branding records)
- Live preview re-renders on every field change (lightweight, no external calls)

## 10. Known Gaps / Limitations

| Gap | Severity | Notes |
|-----|----------|-------|
| No file upload for logo — URL input only | Low | Requires asset management infrastructure not yet built |
| No branding delete action | Low | Backend would need soft-delete support |
| No pagination for branding list | Low | Unlikely to exceed 100 records per tenant |
| No branding duplicate/clone feature | Low | Future convenience feature |
| No drag-and-drop for colour palette | Low | Standard colour pickers used |
| Advanced fields (email header/footer HTML) hidden by default | N/A | Intentional UX choice |
| No backend-rendered preview (only local preview card) | Low | Spec allowed local preview; backend render would require extra setup |
| No breadcrumb navigation from detail back to list | Low | Back button provided |
| Preview card is approximate — actual email rendering may differ | Low | Noted in UI text |

## 11. Issues Encountered

| Issue | Resolution | Status |
|-------|-----------|--------|
| `notifRequest` only supported GET method | Extended with `method` and `body` options | Resolved |
| Error parsing missed nested `{ error: { message, details } }` | Added nested error object parsing | Resolved |
| `BrandingCreateInput` type assertion to `Record<string, unknown>` | Used `as unknown as Record<string, unknown>` for safe bridge | Resolved |
| Server/client boundary violation: client components importing from server-only module | Split shared types into `notifications-shared.ts` (client-safe); client components import from shared module; server module re-exports for backward compatibility | Resolved |

## 12. Next Steps

1. **Tenant template read views** — let tenants see which templates are active for their products
2. **Branding delete action** — allow tenants to remove branding profiles
3. **Logo upload** — replace URL input with file upload when asset management is built
4. **Branding history/audit** — show who changed branding and when
5. **Backend-rendered preview** — use the existing branded preview endpoint for more accurate previews
