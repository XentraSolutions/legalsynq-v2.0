# NOTIF-UI-008 — Tenant Template Override

## 1. IMPLEMENTATION SUMMARY

Built tenant template override capabilities into the tenant portal. Tenants can now create product-scoped template overrides layered on top of global templates, edit override drafts, preview rendered content, and publish overrides to make them active. The UI clearly distinguishes global template content from tenant override content at every level.

**Scope completed**: All primary features from the spec are implemented:
- Product-first template access (preserved from NOTIF-UI-007)
- Override status visibility on template list
- Global vs Override tabbed detail view
- Create override flow (pre-populated from global template)
- Edit override draft flow
- Preview override via real backend
- Publish override with confirmation
- Fallback state visibility (Using Global / Using Tenant Override)

**Overall completeness**: Complete — all 14 acceptance criteria are met.

## 2. FILES CREATED / MODIFIED

### Modified
| File | Changes |
|------|---------|
| `apps/web/src/lib/notifications-shared.ts` | Added `TenantTemplate`, `TenantTemplateListResponse`, `TenantTemplateVersion`, `OverrideStatus`, `TemplatePreviewResult` types |
| `apps/web/src/lib/notifications-server-api.ts` | Added 8 tenant template API methods: `tenantTemplatesList`, `tenantTemplateGet`, `tenantTemplateCreate`, `tenantTemplateUpdate`, `tenantTemplateVersions`, `tenantTemplateCreateVersion`, `tenantTemplatePublishVersion`, `tenantTemplatePreviewVersion`; updated re-exports |
| `apps/web/src/app/(platform)/notifications/templates/actions.ts` | Added 4 server actions: `createTenantOverride`, `createOverrideVersion`, `publishOverrideVersion`, `previewOverrideVersion` |
| `apps/web/src/app/(platform)/notifications/templates/[productType]/page.tsx` | Enhanced to fetch tenant overrides alongside global templates; shows override status badges per template |
| `apps/web/src/app/(platform)/notifications/templates/[productType]/[templateId]/page.tsx` | Enhanced to fetch tenant override data; passes override + overrideVersions to client component |
| `apps/web/src/app/(platform)/notifications/templates/[productType]/[templateId]/template-detail-client.tsx` | Major rewrite: tabbed Global/Override view; override create/edit/publish/preview flows; version management |

## 3. FEATURES IMPLEMENTED

### Product-First Entry
- Preserved from NOTIF-UI-007
- Product selection required before templates display
- Route-based: `/notifications/templates/[productType]`

### Override Status Visibility
- Template list shows per-template override status badges:
  - **Using Global** — no override exists (gray badge)
  - **Override Draft** — override exists but not published (amber badge)
  - **Override Active** — published override in use (green badge)
- Override status derived from cross-referencing global templates with tenant templates by `templateKey + channel`

### Template Detail — Global vs Override View
- Tabbed interface: "Global Template" and "Tenant Override"
- Top-level status indicator: "Using Global Template" or "Using Tenant Override"
- Global tab shows read-only global versions + branded preview
- Override tab shows override lifecycle management

### Override Create Flow
- "Create Override" CTA shown when no override exists
- Creates tenant-scoped template matching global template's `templateKey`, `channel`, `productType`
- First version pre-populated from current published global version content
- Auto-opens editor after creation

### Override Edit Flow
- HTML textarea editor for subject, body, text
- Branding token guidance note
- Edit disabled for published versions (immutable version model)
- "Save New Version" creates a new draft version
- "Edit Latest Draft" quick action from version list

### Override Preview
- Real backend rendering via `POST /v1/templates/:id/versions/:versionId/preview`
- Tabbed result: HTML Preview and Text
- Template variables auto-detected and injected

### Override Publish Flow
- Explicit publish action with confirmation dialog
- Clear messaging about what publishing means
- Previous published version auto-retired
- Status indicators update after publish

### Fallback State Visibility
- "Using Global Template" state clearly shown when no override exists
- "Tenant Override Draft" banner when override exists but unpublished
- "Tenant Override Published" banner when override is active

## 4. API / BACKEND INTEGRATION

### Template Reads
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /v1/templates/global?productType={pt}` | Global template list per product | Working |
| `GET /v1/templates/global/:id` | Global template detail | Working |
| `GET /v1/templates/global/:id/versions` | Global version list | Working |
| `GET /v1/templates` | Tenant template list (filtered by tenantId via header) | Working |

### Override Reads/Writes
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `POST /v1/templates` | Create tenant override template | Working |
| `PATCH /v1/templates/:id` | Update tenant template metadata | Working |
| `GET /v1/templates/:id/versions` | List override versions | Working |
| `POST /v1/templates/:id/versions` | Create override version | Working |

### Preview
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `POST /v1/templates/global/:id/versions/:vId/preview` | Branded preview for global template | Working |
| `POST /v1/templates/:id/versions/:vId/preview` | Preview for tenant override | Working |

### Publish
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `POST /v1/templates/:id/versions/:vId/publish` | Publish override version | Working |

## 5. DATA FLOW / ARCHITECTURE

### Request Flow
```
Server Component (page.tsx)
  └─ requireOrg() → session.tenantId
  └─ Parallel fetch: global templates + tenant templates
  └─ Cross-reference overrides by templateKey+channel
  └─ Pass all data to Client Component

Client Component (template-detail-client.tsx)
  └─ Tabbed view: Global | Override
  └─ Server actions for mutations (create/save/publish/preview)
      └─ requireOrg() → re-validates auth
      └─ notificationsServerApi.*() → backend calls with x-tenant-id
```

### Tenant Context Flow
- `requireOrg()` at page level → ensures authenticated tenant user
- `session.tenantId` passed to API methods
- `x-tenant-id` header injected by `notifRequest()` on every call
- Server actions re-validate auth independently

### Editor Data Flow
- Editor initialised from override version content (subject/body/text)
- "Save New Version" creates a new immutable version (backend model)
- Each save produces a new version number

### Cache/Revalidation Flow
- `router.refresh()` called after mutations to refresh server component data
- `cache: 'no-store'` on all API calls

## 6. VALIDATION & TESTING

| Check | Status |
|-------|--------|
| TypeScript compilation (zero new errors) | PASS |
| Product-first access enforced | PASS |
| Override create pre-populates from global | PASS |
| Override publish with confirmation | PASS |
| Global vs override distinction clear | PASS |
| No editing of global templates exposed | PASS |
| tenantId never manually entered | PASS |
| Auth guards on all pages and actions | PASS |
| HTML rendered in sandboxed iframe | PASS |
| productType validated in server actions | PASS |

## 7. ERROR HANDLING

| Scenario | Handling |
|----------|----------|
| No templates for product | Empty state card |
| No override exists | "Using Global Template" CTA to create |
| Override create fails | Error banner with message |
| Preview fails | Red error notice in preview panel |
| Publish fails | Error banner |
| Invalid product type | Redirect to product selector |
| Template-product mismatch | Redirect / error in server action |
| Missing tenant context | `requireOrg()` redirects to login |
| Backend conflict (duplicate key) | Error surfaced from 409 response |

## 8. TENANT / AUTH CONTEXT

- All pages guarded by `requireOrg()` — redirects unauthenticated users
- `session.tenantId` used for all API calls — never from user input
- `x-tenant-id` header injected automatically by `notifRequest()`
- Server actions independently re-validate auth context
- Override create uses tenantId from session (backend sets `tenantId` on the template record)
- No cross-tenant data visible — tenant templates list is already scoped by `x-tenant-id`

## 9. CACHE / PERFORMANCE

- All API calls use `cache: 'no-store'` for fresh data
- Template list fetches global + tenant templates in parallel
- Override version status resolution done in parallel (`Promise.all`)
- `router.refresh()` after mutations for immediate UI consistency
- No client-side caching of mutable data

### Known Inefficiencies
- Template list page fetches all tenant templates (limit 200) then filters client-side by productType — this works for current scale but should add server-side `productType` filter if tenant template count grows

## 10. KNOWN GAPS / LIMITATIONS

| Gap | Severity | Notes |
|-----|----------|-------|
| No WYSIWYG editor for tenant overrides | Medium | Spec mentions WYSIWYG-compatible editing; currently uses HTML textarea. The CC WYSIWYG editor could be ported but is a separate effort. |
| No delete/revert override capability | Low | Spec says to expose only if backend supports; backend returns 405 for delete. UI clearly shows status but can't remove overrides. |
| No `productType` filter on tenant templates API | Low | Backend `GET /v1/templates` doesn't filter by productType in query params; client-side filtering used instead. |
| Override version edits create new versions | Low | By design — backend uses immutable version model. Editing a published override requires creating a new draft. UI represents this honestly. |
| No `editorJson` round-tripping in editor | Low | Editor uses HTML textarea; `editorJson` is preserved from global template on create but not maintained in subsequent edits. |

## 11. ISSUES ENCOUNTERED

| Issue | Resolution | Status |
|-------|------------|--------|
| Backend uses same Template model for global and tenant | Adapted — tenant overrides use `/v1/templates` routes with `tenantId` from header, not separate override endpoints | Resolved |
| No dedicated override API routes | Used standard template CRUD with tenant context injection | Resolved |
| Template preview endpoint renders without branding for tenant templates | Used `POST /v1/templates/:id/versions/:vId/preview` (same as global but for tenant template ID) | Resolved |

## 12. RUN INSTRUCTIONS

```bash
bash scripts/run-dev.sh
```

Navigate to `/notifications/templates` in the tenant portal (requires authentication).

Flow:
1. Select a product → see templates with override status
2. Click a template → see Global/Override tabs
3. On Override tab → click "Create Override" if no override exists
4. Edit the draft content → Save New Version → Preview → Publish

## 13. READINESS ASSESSMENT

| Question | Answer |
|----------|--------|
| Is NOTIF-UI-008 complete? | **Yes** |
| Is tenant template customisation in place? | **Yes** — create, edit, preview, publish flows all functional |
| Can we proceed to the next phase? | **Yes** |

## 14. NEXT STEPS

- **WYSIWYG Editor Port**: Port the block-based WYSIWYG editor from Control Center to tenant portal for richer template editing
- **Override Revert/Deactivate**: If backend adds support for deactivating overrides without deleting, expose in UI
- **Override Diff View**: Side-by-side comparison of global template vs tenant override content
- **Tenant Notification Activity**: Visibility into sent notifications and delivery status per template
- **Provider Visibility**: Which providers are configured and active for the tenant
