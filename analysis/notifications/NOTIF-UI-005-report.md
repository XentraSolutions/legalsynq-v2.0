# NOTIF-UI-005 Implementation Report

## 1. Implementation Summary

### What was built
NOTIF-UI-005 adds Control Center UI for managing global product templates with WYSIWYG-compatible email workflows and tenant branding administration. The implementation includes:
- Global templates list page with product type and channel filters
- Global template create flow with product type, editor type, and brandable settings
- Global template detail page with metadata editing
- Template version create flow with block-based WYSIWYG editor
- WYSIWYG editor supporting headings, paragraphs, buttons, dividers, images, brand tokens, and custom variables
- Branded preview flow with tenant selection, sample data, and rendered output
- Version publish flow with confirmation
- Tenant branding list page with product type filters
- Tenant branding create and edit forms with color pickers and all branding fields

### Scope completed vs requested scope
All 17 acceptance criteria are fully implemented:
1. Global templates list page works ✅
2. Global template create works ✅
3. Global template metadata edit works ✅
4. Global template detail page works ✅
5. Global template version create flow works ✅
6. Constrained WYSIWYG-compatible editor flow works ✅
7. Branded preview flow works ✅
8. Version publish flow works ✅
9. Tenant branding list page works ✅
10. Tenant branding create works ✅
11. Tenant branding edit works ✅
12. All flows use real backend APIs from NOTIF-008 ✅
13. Tenant/admin auth context remains correct ✅
14. Loading / success / error states are handled ✅
15. Immutable lifecycle constraints are represented honestly in the UI ✅
16. No tenant portal UI is introduced ✅
17. UI compiles/runs cleanly inside the main platform ✅

### Overall completeness assessment
Complete. All acceptance criteria are met.

## 2. Files Created / Modified

### New files
| File | Purpose |
|------|---------|
| `src/app/notifications/templates/global/page.tsx` | Global templates list page with product/channel filters |
| `src/app/notifications/templates/global/[id]/page.tsx` | Global template detail page with versions |
| `src/app/notifications/branding/page.tsx` | Tenant branding list page |
| `src/components/notifications/global-template-create-form.tsx` | Create global template modal |
| `src/components/notifications/global-template-edit-form.tsx` | Edit template metadata modal |
| `src/components/notifications/global-template-version-form.tsx` | Version create form with WYSIWYG/HTML modes |
| `src/components/notifications/global-publish-version-button.tsx` | Publish version with confirmation |
| `src/components/notifications/branded-preview-modal.tsx` | Branded preview with tenant selection |
| `src/components/notifications/wysiwyg-email-editor.tsx` | Block-based WYSIWYG email editor |
| `src/components/notifications/branding-create-form.tsx` | Create tenant branding modal |
| `src/components/notifications/branding-edit-form.tsx` | Edit tenant branding modal |

### Modified files
| File | Changes |
|------|---------|
| `src/lib/notifications-api.ts` | Added `globalTemplates` and `branding` cache tags; added `ProductType`, `TemplateScope`, `EditorType`, `GlobalTemplate`, `GlobalTemplateVersion`, `TenantBranding`, `BrandedPreviewResult` types |
| `src/app/notifications/actions.ts` | Added server actions: `createGlobalTemplate`, `updateGlobalTemplate`, `createGlobalTemplateVersion`, `publishGlobalTemplateVersion`, `previewGlobalTemplateVersion`, `createBranding`, `updateBranding` |
| `src/app/notifications/page.tsx` | Added "Global Templates" and "Tenant Branding" to quick navigation cards |

## 3. Features Implemented

### Global templates list/create/edit
- **List page** at `/notifications/templates/global` shows all global templates in a table
- **Product type filter** — clickable pills to filter by careconnect, synqlien, etc.
- **Channel filter** support via query params
- **Table columns**: template key, name, product type (color-coded badge), channel, editor type, brandable indicator, status, updated timestamp
- **Create modal** with fields: template key, name, product type, channel, editor type, category, description, brandable toggle
- **Edit modal** for metadata: name, description, category, status, brandable toggle
- **Validation**: template key format, required fields, backend conflict errors

### Global template detail/version flow
- **Detail page** at `/notifications/templates/global/[id]`
- Displays all template metadata in a definition list
- Shows product type and brandable badges in the header
- **Versions table** with version number, status, subject, editor type, publish/create timestamps
- Current/published version highlighted with green background
- **Immutable lifecycle notice** displayed prominently: "Versions are immutable after creation. To change content, create a new version and publish it."

### Editor behavior
- **Block-based WYSIWYG editor** that produces both JSON and compiled HTML
- Supported block types: heading (H1/H2/H3), paragraph, button (CTA), divider, image
- **Formatting controls**: bold, italic, underline per block
- **Block management**: add, remove, reorder (up/down)
- **Brand token insertion** via dropdown menu with all reserved tokens
- **Custom variable insertion** with free-text input
- **Editor JSON output**: structured `{ version: 1, blocks: [...] }` format
- **HTML compilation**: each block compiles to semantic HTML
- **Text fallback**: auto-generated plain text from blocks
- **Preview panes**: collapsible compiled HTML preview and editor JSON view
- **HTML mode fallback**: when `editorType` is `html`, shows raw HTML textarea + text fallback textarea

### Preview behavior
- **Branded preview modal** accessible from version actions
- **Tenant ID input** for branding context lookup
- **Product type** auto-filled from template, visible but not editable
- **Template variables** displayed as inputs based on known variable list
- **Preview result tabs**: HTML Preview (rendered), Text (plain), HTML Source (raw)
- **Branding context display**: brand name, primary color swatch, source indicator
- **Fallback notice**: when no tenant ID provided, shows "Default branding applied" message
- Uses real `POST /templates/global/:id/versions/:versionId/preview` endpoint

### Branding admin UI
- **List page** at `/notifications/branding`
- **Product type filter** pills
- **Table columns**: tenant ID (truncated), product type badge, brand name, color swatches, support email, updated timestamp
- **Create modal** with all branding fields: tenant ID, product type, brand name, logo URL, 5 color fields with pickers, button radius, font family, support email/phone, website URL, email header/footer HTML
- **Edit modal** with all editable fields pre-populated
- **Color pickers** with both visual picker and hex text input
- **HTML fields** in collapsible section with clear labeling

### Publish workflow
- **Publish button** shown only for draft versions
- **Two-step confirmation**: click "Publish" → shows "Publish v{n}? Confirm / Cancel"
- Published versions show "Published" label
- Retired versions show status label (not editable)
- After publish: page refreshes to show updated state

## 4. API / Backend Integration

### Global Templates
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /templates/global` | List global templates with filters | Working |
| `POST /templates/global` | Create global template | Working |
| `GET /templates/global/:id` | Get template detail | Working |
| `PATCH /templates/global/:id` | Update template metadata | Working |

### Versions
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /templates/global/:id/versions` | List versions for template | Working |
| `POST /templates/global/:id/versions` | Create new draft version | Working |
| `POST /templates/global/:id/versions/:versionId/publish` | Publish draft version | Working |

### Preview
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `POST /templates/global/:id/versions/:versionId/preview` | Branded preview render | Working |

### Branding
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /branding` | List branding records | Working |
| `POST /branding` | Create tenant branding | Working |
| `PATCH /branding/:id` | Update tenant branding | Working |

## 5. Data Flow / Architecture

### Request flow
1. Server components call `notifClient.get()` with cache tags for SSR data
2. Requests go through the gateway: `CONTROL_CENTER_API_BASE/notifications/v1/...`
3. Bearer auth from `platform_session` cookie injected automatically
4. Response parsed with `{ data: ... }` envelope handling

### Server action flow
1. Client components call server actions via `useTransition`
2. Actions call `requirePlatformAdmin()` for auth
3. Actions use `notifClient.post()` / `notifClient.patch()`
4. On success: `revalidateTag()` to invalidate cache
5. Return `ActionResult<T>` to client

### Editor data flow (editorJson + compiled HTML/body)
1. User edits blocks in `WysiwygEmailEditor`
2. On every change, `onChange(json, html, text)` callback fires
3. `json`: `{ version: 1, blocks: [...] }` — structured source of truth
4. `html`: compiled from blocks via `blockToHtml()` — renderable email body
5. `text`: compiled from blocks via `blockToText()` — plain text fallback
6. Parent form stores all three; submits `editorJson` (stringified), `bodyTemplate` (html), `textTemplate` (text)

### Preview flow
1. Admin enters tenant ID + sample template data
2. Form calls `previewGlobalTemplateVersion` server action
3. Action posts to `/templates/global/:id/versions/:versionId/preview` with `{ tenantId, productType, templateData }`
4. Backend resolves branding, injects tokens, renders template
5. Response contains rendered subject/body/text + branding metadata
6. Modal displays rendered output in HTML/text/source tabs

### Branding flow
1. List page fetches via `GET /branding` with product type filter
2. Create/edit forms submit via server actions
3. Actions call `POST /branding` or `PATCH /branding/:id`
4. On success: `revalidateTag(NOTIF_CACHE_TAGS.branding)`

### Cache revalidation flow
| Action | Tag Invalidated |
|--------|----------------|
| `createGlobalTemplate` | `notif:global-templates` |
| `updateGlobalTemplate` | `notif:global-templates` |
| `createGlobalTemplateVersion` | `notif:global-templates` |
| `publishGlobalTemplateVersion` | `notif:global-templates` |
| `createBranding` | `notif:branding` |
| `updateBranding` | `notif:branding` |
| `previewGlobalTemplateVersion` | none (read-like) |

## 6. Validation & Testing

### Typecheck / compile status
- **PASS** — All new/modified files compile cleanly
- 11 pre-existing errors in other files remain unchanged (not introduced by NOTIF-UI-005)

### Manual test flows
- Global templates list: product filter pills cycle through products — PASS
- Global template create: full form with all fields, validation errors shown — PASS
- Template detail: metadata display, edit modal — PASS
- Version create with WYSIWYG: blocks add/remove/reorder, tokens insert — PASS
- Version create with HTML mode: raw HTML + text textarea — PASS
- Branded preview: tenant input, sample data, render result tabs — PASS
- Version publish: confirmation flow — PASS
- Branding list: product filter, color swatches — PASS
- Branding create: full form with color pickers — PASS
- Branding edit: pre-populated fields, update — PASS

### Edge cases handled
| Edge Case | Status |
|-----------|--------|
| Empty global templates list | PASS — shows "No global templates found" with filter hint |
| Empty branding list | PASS — shows "No branding records found" with filter hint |
| Empty versions list | PASS — shows "No versions yet. Create one to get started." |
| WYSIWYG editor with no blocks | PASS — shows "Add first block" |
| Template key format validation | PASS — lowercase alphanumeric check |
| Backend conflict errors (duplicate) | PASS — error message displayed |
| Preview without tenant ID | PASS — shows default branding notice |
| Non-draft version publish attempt | PASS — shows status label, no button |
| Already-published version | PASS — shows "Published" label |

## 7. Error Handling

### Editor validation
- Empty editor (no blocks/no HTML) — blocked with error message
- Missing subject for email channel — blocked with error message
- WYSIWYG requires editorJson to be present with content

### Preview failures
- Backend 422 (missing variables) — error message shown
- Network errors — caught and displayed
- Empty tenant ID — preview still works with fallback branding + notice

### Backend error handling
- API errors parsed from response body (message/title/error fields)
- 409 conflicts shown clearly (duplicate template key, duplicate branding)
- 404 not found — "Template not found" empty state
- 401 — redirect to login (handled by notifFetch)

### Unsupported version editing behavior
- Prominent notice: "Versions are immutable after creation"
- No edit buttons shown for any version status
- Only "New Version" button available

### Empty/no-branding states
- Preview without tenant branding — shows "Default branding applied" notice
- Branding list empty — shows empty state message
- Templates list empty — shows empty state with filter hint

## 8. Tenant / Auth Context

### How auth is enforced
- All pages call `requirePlatformAdmin()` — redirects to login if not platform admin
- All server actions call `requirePlatformAdmin()` before any operation
- Bearer token from `platform_session` cookie injected on all API calls

### How tenant/admin context is handled for preview/branding
- **Preview**: admin enters tenant ID manually (this is a Control Center flow, not tenant-bound)
- **Branding create**: admin enters tenant ID manually to configure branding for a specific tenant
- **Branding edit**: tenant ID comes from the existing record (not editable)
- Backend branding endpoints enforce tenant scoping via `req.tenantId` from gateway context

### How required headers/tokens are injected
- `notifFetch` automatically adds `Authorization: Bearer ${platform_session}`, `Content-Type: application/json`, `X-Request-Id`
- Branding create/update actions inject `x-tenant-id` header via `extraHeaders` so the backend can resolve `req.tenantId`
- Branding list does NOT send `x-tenant-id` — platform admin sees all branding records (repository treats tenantId as optional filter)

### Behavior when tenant context is missing
- Preview works with fallback/default branding — notice shown
- Branding create requires explicit tenant ID — validation error if missing

## 9. Cache / Performance

### Cache tags invalidated
| Tag | Actions |
|-----|---------|
| `notif:global-templates` | Create/update template, create version, publish version |
| `notif:branding` | Create/update branding |

### Revalidation approach
- ISR with 60-second revalidation for list/detail fetches
- Manual revalidation via `revalidateTag()` after mutations
- Preview is no-store (no caching — always fresh)

### Performance considerations
- Global templates list fetches all templates (no server-side pagination yet)
- Branding list limited to 100 records
- WYSIWYG editor is lightweight (no heavy library dependency)
- Preview renders server-side via action (no client-side rendering)

## 10. Known Gaps / Limitations

| Gap | Severity | Future Phase |
|-----|----------|-------------|
| No server-side pagination for global templates list | Low | Add when template count grows |
| No rich text formatting within paragraph blocks (inline bold/italic) | Low | Future editor enhancement |
| No image upload — logo URL input only | Low | Requires asset management infrastructure |
| No template duplication/clone feature | Low | Future convenience feature |
| No template delete action | Low | May need soft-delete support |
| No branding delete action | Low | Future |
| No version content viewer (only preview) | Low | Could add read-only content modal |
| WYSIWYG editor is block-based, not inline-rich | Medium | Could integrate TipTap/ProseMirror for richer editing |
| No drag-and-drop block reordering | Low | Arrow buttons used instead |
| No tenant picker/search for preview — manual UUID entry | Low | Could add tenant search |
| No branding preview card on branding edit page | Low | Future enhancement |
| 11 pre-existing TS errors in other files | Low | Not introduced by NOTIF-UI-005 |

## 11. Issues Encountered

| Issue | Resolution | Status |
|-------|-----------|--------|
| Backend `{ data: ... }` envelope needs handling in SSR fetches | Added fallback parsing: `(res as { data: T }).data ?? res` | Resolved |
| Preview response shape differs from initial type assumption | Updated `BrandedPreviewResult` to match actual backend response (flat subject/body/text + branding object) | Resolved |
| WYSIWYG editor needs to produce both JSON and HTML simultaneously | Built block-based editor with `onChange(json, html, text)` callback pattern | Resolved |
| Brand token menu needs to avoid stealing focus from textarea | Used refs + deferred focus restoration after token insertion | Resolved |
| Version form needs to support both WYSIWYG and HTML modes | Conditional rendering based on template's `editorType` field | Resolved |

## 12. Run Instructions

### Start the system
```bash
bash scripts/run-dev.sh
```

### Access the features
- **Global Templates**: Navigate to `/notifications/templates/global` in the Control Center (port 5004)
- **Tenant Branding**: Navigate to `/notifications/branding` in the Control Center
- **From Notifications Overview**: Use the quick navigation cards for "Global Templates" and "Tenant Branding"

### Test flows
1. **Create Global Template**:
   - Click "New Global Template" on the list page
   - Fill in key, name, product type, channel, editor type
   - Toggle brandable if desired
   - Submit → redirects to detail page

2. **Create Version (WYSIWYG)**:
   - On template detail page, click "New Version"
   - Enter subject template (e.g., `{{brand.name}} - Appointment Confirmed`)
   - Add blocks: heading, paragraph with brand tokens, button CTA
   - Review compiled HTML preview
   - Submit → version appears in versions table

3. **Publish Version**:
   - Click "Publish" on a draft version
   - Confirm in the confirmation prompt
   - Version becomes current; previous published version retires

4. **Branded Preview**:
   - Click "Preview" on any version of a brandable template
   - Enter a tenant UUID (or leave empty for defaults)
   - Add sample values for template variables
   - Click "Render Preview"
   - View result in HTML/Text/Source tabs

5. **Create Branding**:
   - Navigate to `/notifications/branding`
   - Click "New Branding"
   - Enter tenant ID, product type, brand name
   - Set colors using pickers
   - Add support email/phone
   - Submit

6. **Edit Branding**:
   - Click "Edit →" on any branding record
   - Modify fields
   - Save

## 13. Readiness Assessment

- **Is NOTIF-UI-005 complete?** Yes
- **Is the Control Center now complete enough to start tenant portal work?** Yes — all admin-side template and branding management is in place
- **Can we proceed to the next phase?** Yes

## 14. Next Steps

### Next features to build
1. **Tenant Portal branding self-service** — allow tenants to manage their own branding
2. **Tenant Portal template read views** — let tenants see which templates are active for their products
3. **Template content viewer** — read-only view of version content without going through preview
4. **Tenant search/picker** — improve UX for preview and branding management with tenant search
5. **Richer WYSIWYG editor** — integrate TipTap or ProseMirror for inline formatting and more block types

### Remaining backend dependencies
- Tenant portal auth middleware for tenant-scoped branding access
- Tenant-scope template overrides (future backend feature)
- Asset management for logo uploads (future backend feature)

### Should tenant branding self-service or tenant template read views come next?
Tenant branding self-service should come first — it has higher immediate value and is simpler to implement since the backend already supports tenant-scoped branding CRUD.
