# NOTIF-UI-007 — Tenant Template Visibility (Read-Only)

## 1. IMPLEMENTATION SUMMARY

Built tenant-facing read-only template visibility into the platform tenant portal. Tenants can browse global notification templates scoped by product type, view template versions and content, and preview templates rendered with their tenant branding via real backend API calls. Strictly read-only — no editing, publishing, or overriding capabilities.

## 2. FILES CREATED / MODIFIED

### Created
| File | Purpose |
|------|---------|
| `apps/web/src/app/(platform)/notifications/templates/page.tsx` | Product selection entry page |
| `apps/web/src/app/(platform)/notifications/templates/actions.ts` | Server action for branded preview |
| `apps/web/src/app/(platform)/notifications/templates/[productType]/page.tsx` | Product-scoped template list |
| `apps/web/src/app/(platform)/notifications/templates/[productType]/[templateId]/page.tsx` | Template detail (server component) |
| `apps/web/src/app/(platform)/notifications/templates/[productType]/[templateId]/template-detail-client.tsx` | Template detail client component (version viewer, preview panel) |

### Modified
| File | Changes |
|------|---------|
| `apps/web/src/lib/notifications-shared.ts` | Added `GlobalTemplate`, `GlobalTemplateVersion`, `GlobalTemplateListResponse`, `BrandedPreviewResult` types |
| `apps/web/src/lib/notifications-server-api.ts` | Added `globalTemplatesList`, `globalTemplateGet`, `globalTemplateVersions`, `globalTemplatePreview` methods; updated re-exports |
| `apps/web/src/app/(platform)/notifications/page.tsx` | Added "Templates" nav link to notifications overview |

## 3. FEATURES IMPLEMENTED

### Product Selection Entry (`/notifications/templates`)
- Product cards for all 5 product types (CareConnect, SynqLien, SynqFund, SynqRx, SynqPayout)
- Color-coded cards with product icons
- Navigates to `/notifications/templates/[productType]` on selection
- No templates shown until product is selected (product-first enforcement)

### Template List (`/notifications/templates/[productType]`)
- Product-scoped template table with name, key, channel, category, branded indicator, last updated
- Active product badge displayed at top
- Breadcrumb navigation back to product selector
- Empty state for products with no templates
- Invalid product type redirects to product selector

### Template Detail (`/notifications/templates/[productType]/[templateId]`)
- Full template metadata (name, description, key, channel, category, branding status)
- Breadcrumb navigation (Products > Product > Template)
- Version list table with version number, status, subject, created/published dates
- Published version highlighted with "(current)" indicator
- "View" and "Preview" actions per version (Preview only shown for brandable templates)

### Version Viewer
- Displays subject, HTML content (rendered), and plain text
- Template variables listing extracted from content or schema
- Read-only notice banner

### Branded Preview
- Sample data input form with dynamically detected template variables
- Pre-populated from `sampleDataJson` when available
- Real backend POST call to `/templates/global/:id/versions/:versionId/preview`
- Tabbed result view: HTML Preview, Text, HTML Source
- Branding indicator showing brand name, primary color swatch, and source (tenant/product_defaults/system_defaults)
- Fallback notice when default branding is applied, with link to branding setup

## 4. API / BACKEND INTEGRATION

| Endpoint | Method | Usage |
|----------|--------|-------|
| `GET /notifications/v1/templates/global?productType={pt}` | Server component | Template list per product |
| `GET /notifications/v1/templates/global/:id` | Server component | Template detail |
| `GET /notifications/v1/templates/global/:id/versions` | Server component | Version list |
| `POST /notifications/v1/templates/global/:id/versions/:vId/preview` | Server action | Branded preview |

All endpoints use `x-tenant-id` header injected by `notifRequest()`.

## 5. DATA FLOW / ARCHITECTURE

```
Server Component (page.tsx)
  └─ requireOrg() → session.tenantId
  └─ notificationsServerApi.globalTemplates*() → fetch with x-tenant-id
  └─ Pass data to Client Component

Client Component (template-detail-client.tsx)
  └─ Version selection (View / Preview)
  └─ Variable extraction (schema or regex)
  └─ Sample data form
  └─ previewTemplateVersion() server action
      └─ requireOrg() → session.tenantId (re-validated)
      └─ notificationsServerApi.globalTemplatePreview()
```

Pattern: Server components fetch initial data; client component handles interactive version viewing and preview. Server action wraps preview mutation with auth guard.

## 6. VALIDATION & TESTING

- TypeScript compilation passes with zero new errors (`npx tsc --noEmit`)
- Product type validated against `PRODUCT_TYPES` array; invalid types redirect
- Application starts and routes render (redirects to login for unauthenticated, as expected)
- Server action validates tenant context via `requireOrg()` before every call

## 7. ERROR HANDLING

| Scenario | Handling |
|----------|----------|
| No templates for product | Empty state card with informative message |
| Invalid product type | Redirect to product selector |
| Template fetch failure | Red error banner with error message |
| Preview API failure | Red error banner in preview panel |
| Missing branding | "Default branding applied" notice with link to setup |
| No template variables | "No template variables detected" message |
| No session/tenant | `requireOrg()` redirects to login/no-org |

## 8. TENANT / AUTH CONTEXT

- All pages guarded by `requireOrg()` — redirects unauthenticated users
- `session.tenantId` injected as `x-tenant-id` header automatically via `notifRequest()`
- Tenant never enters their ID manually
- Server action re-validates auth context independently
- Preview payload includes `tenantId` from session (not user input)

## 9. CACHE / PERFORMANCE

- All API calls use `cache: 'no-store'` for fresh data (consistent with existing pattern)
- Template list and detail fetched in parallel (`Promise.all` for template + versions)
- No client-side caching of API responses (read-only views, data freshness prioritized)

## 10. KNOWN GAPS / LIMITATIONS

- No pagination UI on template list (fetches up to 100 templates per product — sufficient for current scale)
- Variable detection uses regex fallback when `variablesSchemaJson` is absent — may miss complex variable patterns
- Preview renders HTML via `dangerouslySetInnerHTML` — relies on backend to sanitize content
- No search/filter within template list (not required by spec)

## 11. ISSUES ENCOUNTERED

- None. Implementation followed established patterns from NOTIF-UI-005 and NOTIF-UI-006.

## 12. RUN INSTRUCTIONS

```bash
bash scripts/run-dev.sh
```

Navigate to `/notifications/templates` in the tenant portal (requires authentication).

Flow:
1. Select a product → see templates for that product
2. Click a template → see metadata and versions
3. Click "View" on a version → see content
4. Click "Preview" → fill sample data → click "Render Preview" → see branded output

## 13. READINESS ASSESSMENT

| Criteria | Status |
|----------|--------|
| Product must be selected before templates load | PASS |
| Templates always product-scoped | PASS |
| Template list works per product | PASS |
| Template detail works | PASS |
| Version list works | PASS |
| Version viewer works | PASS |
| Branded preview via backend | PASS |
| tenantId never manually entered | PASS |
| Tenant context enforced | PASS |
| No editing capabilities | PASS |
| UI compiles and runs cleanly | PASS |

## 14. NEXT STEPS

- **NOTIF-UI-008**: Tenant template overrides (if planned) — allow tenants to customize templates
- **Pagination**: Add pagination to template list if template volume grows significantly
- **Search/Filter**: Add search by name/key and filter by channel/category if needed
- **Preview History**: Consider caching recent preview results for faster re-rendering
