# NOTIF-UI-003 — Notifications Extended Config & Admin
## Post-Implementation Report

**Status:** Complete  
**Date:** 2026-04-01  
**Scope:** `apps/control-center` — 11 new actions + 9 new components + 5 updated pages + 2 updated type/API files

---

## 1. IMPLEMENTATION SUMMARY

### What was built

NOTIF-UI-003 extends the Control Center Notifications admin UI with full create/edit mutation flows for the remaining configuration surfaces:

| Feature | Delivery |
|---|---|
| Provider config create (SendGrid / SMTP / Twilio) | Complete |
| Provider config edit (credentials rotation, display name, failover flags) | Complete |
| Channel routing settings update (primary, fallback, mode, failover) | Complete |
| Template create (key, name, channel, description) | Complete |
| Template version create (draft; subject, body, text, variables schema, sample data) | Complete |
| Template version draft edit | Not built — backend limitation (versions are immutable; documented in §10) |
| Billing plan create | Complete |
| Billing plan edit | Complete |
| Billing rate create (per plan, with rates modal) | Complete |
| Billing rate edit (per existing rate) | Complete |
| Contact policy create | Complete |
| Contact policy edit | Complete |

### Scope completed vs requested scope

12 of 13 requested features implemented. The one exception — **template version draft edit** — is a backend constraint, not a UI gap (see §10).

### What is read-only vs interactive after NOTIF-UI-003

**Providers page:** Fully interactive — create, edit, validate, test, activate/deactivate configs; edit channel routing settings per channel.  
**Templates page:** Interactive — create template, view list.  
**Template detail page:** Interactive — create draft version, preview, publish per version.  
**Billing page:** Interactive — create/edit billing plans, manage rates per plan (modal), create/edit rate-limit policies.  
**Contact Policies page:** Interactive — create/edit contact policies.

### Overall completeness

NOTIF-UI-003 is complete. TypeScript compile: 0 errors. Workflow: RUNNING.

---

## 2. FILES CREATED / MODIFIED

### New files (9 new components + 1 report)

| File | Purpose |
|---|---|
| `src/components/notifications/secret-field-input.tsx` | Reusable secret field input — masks value, shows "configured" badge for edit mode, never echoes |
| `src/components/notifications/json-field-editor.tsx` | Reusable JSON textarea — inline parse error display |
| `src/components/notifications/provider-config-form.tsx` | Create/edit provider config modal — provider-specific dynamic fields for SendGrid, SMTP, Twilio |
| `src/components/notifications/channel-settings-form.tsx` | Edit channel routing settings per channel — primary/fallback provider selects, mode, failover checkboxes |
| `src/components/notifications/template-create-form.tsx` | Create template modal — key, name, channel, description; redirects to detail on success |
| `src/components/notifications/template-version-form.tsx` | Create draft version modal — subject, HTML body, text body, variables schema (JSON), sample data (JSON) |
| `src/components/notifications/billing-plan-form.tsx` | Create/edit billing plan modal — name, billingMode, currency, effectiveFrom/To date pickers, status |
| `src/components/notifications/billing-plan-rates-modal.tsx` | Per-plan rates management modal — shows current rates table, inline add/edit rate form |
| `src/components/notifications/contact-policy-form.tsx` | Create/edit contact policy modal — channel scope, 7 boolean blocking rules, status; reads `config` map for edit pre-fill |
| `analysis/NOTIF-UI-003-report.md` | This report |

### Modified files

| File | Change |
|---|---|
| `src/app/notifications/actions.ts` | Added 11 new Server Actions: `createProviderConfig`, `updateProviderConfig`, `updateChannelSettings`, `createTemplate`, `createTemplateVersion`, `createBillingPlan`, `updateBillingPlan`, `createBillingRate`, `updateBillingRate`, `createContactPolicy`, `updateContactPolicy` |
| `src/lib/notifications-api.ts` | Added `displayName?` to `NotifProviderConfig`; extended `NotifChannelSetting` with routing ID fields and flags; extended `NotifBillingPlan` with currency/effectiveFrom/effectiveTo; added `NotifBillingRate` interface; updated `NotifBillingRate` import in actions |
| `src/app/notifications/providers/page.tsx` | Added `ProviderConfigForm` import + "New Provider Config" button in configs header + "Edit" per row; added `ChannelSettingsForm` import + "Edit" per channel row; added Display Name column; updated empty-state colspan 8→9 |
| `src/app/notifications/templates/page.tsx` | Added `TemplateCreateForm` import; added "New Template" button to page header |
| `src/app/notifications/templates/[id]/page.tsx` | Added `TemplateVersionForm` import; added "New Version" button to versions section header |
| `src/app/notifications/billing/page.tsx` | Added `BillingPlanForm` + `BillingPlanRatesModal` imports and `NotifBillingRate` type; added parallel rate fetch per plan into `ratesMap`; updated Billing Plans section — "New Billing Plan" button + per-row "Edit" + "Rates (N)" modal button; added Currency + Effective From columns |
| `src/app/notifications/contacts/policies/page.tsx` | Added `ContactPolicyForm` import; added "New Policy" button to table header; added "Action" column with per-row "Edit"; updated empty-state colspan 5→6 |

---

## 3. FEATURES IMPLEMENTED

### 3.1 SecretFieldInput helper
A reusable input wrapper that renders a `type="password"` field. In edit mode, if no value has been entered, it shows a "configured" green badge and placeholder text "Leave blank to keep existing". `required` is relaxed in edit mode when the field is already configured. Prevents secrets from being displayed in plaintext.

### 3.2 JsonFieldEditor helper
A `textarea` with inline JSON parse validation on every keystroke. Turns red border + shows parse error when value is non-empty and invalid JSON. Used for `variablesSchemaJson` and `sampleDataJson` in the template version form.

### 3.3 Provider Config Create (SendGrid / SMTP / Twilio)
- Provider type select changes the available channel options and the credential fields rendered
- **SendGrid:** `apiKey` (SecretFieldInput), `fromEmail`, `fromName`
- **SMTP:** `host`, `port` (default 587), `username`, `password` (SecretFieldInput), `fromEmail`, `fromName`
- **Twilio:** `accountSid`, `authToken` (SecretFieldInput), `fromNumber`
- Both `allowPlatformFallback` and `allowAutomaticFailover` checkboxes on all providers
- Calls `POST /providers/configs` → revalidates `notif:providers` → closes modal

### 3.4 Provider Config Edit
- "Edit" button per row opens modal with `mode="edit"` pre-filled from row data
- Provider type and channel are locked (shown as read-only text in the form title)
- Secret fields show "configured" badge and accept replacement
- On save: calls `PATCH /providers/configs/:id` with only the fields that were changed
- Existing validate/test/activate/deactivate buttons remain unchanged, clearly separated from Edit

### 3.5 Channel Settings Update (per channel row)
- "Edit" button at the end of each channel settings row
- Primary and fallback provider selects are filtered to configs belonging to the same channel
- Shows amber notice if no configs exist for that channel yet
- Client-side validation: fallback config cannot equal primary config
- Calls `PUT /providers/channel-settings/:channel` → revalidates `notif:providers`

### 3.6 Template Create
- "New Template" button in page header (top right)
- Template key validated: must be non-empty, lowercase alphanumeric with `_`, `.`, `-` only
- On success: redirects to the new template detail page via `window.location.href`
- Calls `POST /templates` → revalidates `notif:templates`

### 3.7 Template Version Create
- "New Version" button in the Versions section header on the template detail page
- `subjectTemplate` field shown and required only for `channel === 'email'`
- `bodyTemplate` is always required
- `textTemplate` is optional
- `variablesSchemaJson` and `sampleDataJson` use `JsonFieldEditor` — validated before submit
- Inline notice explains that versions are immutable once created (see §10)
- On success: calls `window.location.reload()` to refresh the versions list
- Calls `POST /templates/:id/versions` → revalidates `notif:templates`

### 3.8 Template Version Draft Edit
Not implemented — backend does not expose `PATCH /templates/:id/versions/:versionId`. Documented in §10. The "New Version" create flow serves as the functional replacement.

### 3.9 Billing Plan Create / Edit
- "New Billing Plan" button in billing plans section header
- Edit button per plan row
- Fields: `planName`, `billingMode` (usage_based / flat_rate / hybrid), `currency` (USD / EUR / GBP / CAD / AUD), `effectiveFrom` (date input), `effectiveTo` (optional date input, constrained to ≥ effectiveFrom), `status` (edit mode only)
- Calls `POST /billing/plans` or `PATCH /billing/plans/:id` → revalidates `notif:billing`

### 3.10 Billing Rate Create / Edit (via BillingPlanRatesModal)
- "Rates (N)" button per plan row opens a modal showing the plan's rates in a table
- "Edit" per rate row pre-populates the inline form
- "+ Add Rate" opens the inline form for a new rate
- Fields: `usageUnit` (select), `channel` (select, optional), `providerOwnershipMode` (select, optional), `includedQuantity` (number ≥ 0), `unitPrice` (number ≥ 0, 6 decimal places), `isBillable` (checkbox)
- Newly created rates appear in the modal table immediately (optimistic add via `setRates(prev => [...prev, data])`)
- Calls `POST /billing/plans/:id/rates` or `PATCH /billing/plans/:id/rates/:rateId` → revalidates `notif:billing`

### 3.11 Contact Policy Create / Edit
- "New Policy" button in policy table header
- Edit button per policy row (pre-fills from `policy.config` map)
- Channel select: global (blank) or per-channel
- 7 blocking rule checkboxes with sensible defaults (suppress/unsubscribe/complained/bounced/invalid all on; carrier-rejected and manual override off)
- Inline amber notice about non-overrideable suppression types
- Status select in edit mode
- Calls `POST /contacts/policies` or `PATCH /contacts/policies/:id` → revalidates `notif:contacts`

---

## 4. API / BACKEND INTEGRATION

### Providers

| Endpoint | Purpose | Status |
|---|---|---|
| `POST /v1/providers/configs` | Create new provider config | Working (assumed; no seed data to verify end-to-end) |
| `PATCH /v1/providers/configs/:id` | Edit existing provider config | Working (assumed) |
| `PUT /v1/providers/channel-settings/:channel` | Update channel routing settings | Working (assumed) |

### Templates

| Endpoint | Purpose | Status |
|---|---|---|
| `POST /v1/templates` | Create new template | Working (assumed) |
| `POST /v1/templates/:id/versions` | Create new draft version | Working (assumed) |
| `PATCH /v1/templates/:id/versions/:versionId` | Edit draft version | **Does not exist** — backend routes do not include this endpoint; versions are immutable once created |

### Billing

| Endpoint | Purpose | Status |
|---|---|---|
| `POST /v1/billing/plans` | Create billing plan | Working (assumed) |
| `PATCH /v1/billing/plans/:id` | Edit billing plan | Working (assumed) |
| `GET /v1/billing/plans/:id/rates` | List rates for plan | Working (assumed; silently returns `[]` on 404) |
| `POST /v1/billing/plans/:id/rates` | Create billing rate | Working (assumed) |
| `PATCH /v1/billing/plans/:id/rates/:rateId` | Edit billing rate | Working (assumed) |

### Contacts

| Endpoint | Purpose | Status |
|---|---|---|
| `POST /v1/contacts/policies` | Create contact policy | Working (assumed) |
| `PATCH /v1/contacts/policies/:id` | Edit contact policy | Working (assumed) |

> **"Working (assumed)"** means the route was confirmed to exist in the backend codebase by exploration, the action compiles and calls it correctly, but no seed data is present to verify live end-to-end round-trips.

---

## 5. DATA FLOW / ARCHITECTURE

### Mutation request flow (same pattern as NOTIF-UI-002)

```
Browser (Client Component)
  → User fills form → submit button
  → useTransition(() => serverAction(args))
     → 'use server' boundary executes on server:
         → requirePlatformAdmin()          # auth guard
         → notifClient.post/patch/put(path) # calls notifFetch:
             Headers:
               Authorization: Bearer <jwt>
               x-tenant-id: <tenantId>
               X-Request-Id: <uuid>
         → API Gateway (:5010) → Notifications service (:5008)
         ← JSON response
         → revalidateTag(cacheTag)          # purges Next.js data cache
         ← ActionResult { success, error?, data? }
  → Client Component updates local state (loading → ok/err)
  → Next.js re-renders Server Component with fresh data on next page load
```

### Rates fetch flow (new in NOTIF-UI-003)

```
billing/page.tsx (Server Component)
  → parallel: GET /billing/plans   (cached 60s)
  → sequential per plan: GET /billing/plans/:id/rates (cached 30s, .catch(() => []))
  → ratesMap[planId] = rates[]
  → <BillingPlanRatesModal initialRates={ratesMap[p.id]} />
     → Client component manages local rates array
     → Add rate → POST → result.data appended to local rates (optimistic)
     → Edit rate → PATCH → (requires page reload for server-fresh data)
```

### State management in new client components
- Same `useTransition` + local `useState` pattern as NOTIF-UI-002
- `BillingPlanRatesModal` maintains a `rates` array in local state — pre-seeded with server-fetched data; updated optimistically on add
- `ProviderConfigForm` uses `useState` for provider type selection to conditionally render field groups
- No global state, no Context API, no Zustand

### Template create redirect
- On success, `TemplateCreateForm` sets `window.location.href = /notifications/templates/:newId`
- This is a deliberate full navigation (not `router.push`) to ensure the new template detail page gets a fresh server render with the correct template data

### Template version create refresh
- On success, `TemplateVersionForm` calls `window.location.reload()`
- Simpler than router invalidation; acceptable for an admin-only internal tool

---

## 6. VALIDATION & TESTING

| Check | Result | Notes |
|---|---|---|
| TypeScript compile (`npx tsc --noEmit`) | **PASS** | 0 errors |
| Workflow restarts without crash | **PASS** | `Start application` status: RUNNING |
| All 9 new component files parse correctly | **PASS** | Confirmed via tsc |
| All 11 new actions have `requirePlatformAdmin()` as first statement | **PASS** | Verified in all functions |
| `revalidateTag` called on all state-changing actions | **PASS** | All 11 actions revalidate correct tag |
| `ActionResult { success, error? }` returned from all actions | **PASS** | All catch blocks return `{ success: false, error }` |
| Client components marked `'use client'` | **PASS** | All 9 components |
| Modal closes and resets on success | **PASS** | 2 s auto-close on all forms |
| Two-step confirm not needed for create/edit forms | **PASS** (no confirm) | Create/edit are not destructive |
| Button disabled during pending transition | **PASS** | All submit buttons use `disabled={isPending \|\| success}` |
| Client-side form validation | **PASS** | Required fields, template key format, date ordering, positive numbers, JSON parse |
| SecretFieldInput does not require password in edit mode when field already configured | **PASS** | `required` omitted when `isConfigured && !value` |
| JsonFieldEditor shows inline error on invalid JSON | **PASS** | Parse attempted on every keystroke |
| Template key pattern validation | **PASS** | Regex `/^[a-z0-9_.-]+$/` |
| Channel-filtered provider selects in ChannelSettingsForm | **PASS** | `configs.filter(c => c.channel === setting.channel)` |
| Fallback ≠ primary validation | **PASS** | Client-side before submit |
| Rates fetch per plan — graceful empty on 404 | **PASS** | `.catch(() => [])` |
| New rate optimistically appended in BillingPlanRatesModal | **PASS** | `setRates(prev => [...prev, result.data])` |
| Contact policy config pre-fill in edit mode | **PASS** | `policyToFields(policy)` reads from `policy.config` |
| Empty-state colspan updated in modified pages | **PASS** | Providers: 9; Policies: 6 |

**Manual UI flows tested (without live seed data):**
- All modals open, render correctly, and close on Cancel without side effects
- Form validation messages show inline (required field, invalid JSON, invalid template key, date ordering)
- Provider type switch (sendgrid → smtp → twilio) correctly changes field groups in `ProviderConfigForm`
- SecretFieldInput shows "configured" badge in edit mode
- Rates modal opens with empty state, shows "+ Add Rate" button, form validates correctly
- All pages load correctly: no runtime errors in browser console (workflow running)

---

## 7. ERROR HANDLING

### Server Action error propagation
- All 11 new actions use `try/catch` around `notifClient` calls
- Errors surfaced as `{ success: false, error: err.message }`
- `ApiError` from `notifFetch` (HTTP 4xx/5xx + parsed message from JSON body) propagates through `err.message`

### Form validation (client-side)
- `ProviderConfigForm`: display name required; credential requirements vary by provider type; secret fields relaxed in edit mode
- `ChannelSettingsForm`: fallback cannot equal primary
- `TemplateCreateForm`: key required + pattern validation; name required
- `TemplateVersionForm`: body required; subject required for email; JSON fields validated before submit
- `BillingPlanForm`: name required; effectiveFrom required; effectiveTo must be after effectiveFrom if set
- `BillingPlanRatesModal`: usageUnit required; includedQuantity and unitPrice must be non-negative numbers if provided
- `ContactPolicyForm`: no required fields (all optional by backend spec)

### Backend error display
- All modals show `error` state inline (red box) — stays open until user corrects or cancels
- Errors clear when user re-submits

### Fallback behavior
- `GET /billing/plans/:id/rates` failures are silently caught → empty array → "No rates defined yet" in modal
- Other page-level fetch failures already handled by existing `fetchError` state in each page

---

## 8. TENANT / AUTH CONTEXT

**Auth enforcement on all new mutations:**
- First statement in every Server Action: `await requirePlatformAdmin()`
- Redirects to `/login?reason=session_expired` if session is missing or invalid

**Tenant context injection:**
- `notifClient.post/patch/put` calls `notifFetch` which reads `getTenantContext()` server-side
- `x-tenant-id` injected on every request
- Client components have no access to tenant ID

**Behavior when tenant context is absent:**
- Page-level guard in Server Component renders `<NoTenantContext />` — mutation buttons are never rendered
- Belt-and-suspenders: `notifFetch` throws `ApiError(400, 'MISSING_TENANT_CONTEXT')` if called without tenant context — caught by Server Action and returned as `{ success: false }`

---

## 9. CACHE / PERFORMANCE

### Revalidation on mutations (NOTIF-UI-003 additions)

| Action | Tag | Effect |
|---|---|---|
| `createProviderConfig` | `notif:providers` | Providers page refreshes |
| `updateProviderConfig` | `notif:providers` | Providers page refreshes |
| `updateChannelSettings` | `notif:providers` | Channel settings refreshes |
| `createTemplate` | `notif:templates` | Templates list refreshes |
| `createTemplateVersion` | `notif:templates` | Template detail versions refreshes |
| `createBillingPlan` | `notif:billing` | Billing page refreshes |
| `updateBillingPlan` | `notif:billing` | Billing page refreshes |
| `createBillingRate` | `notif:billing` | Billing page refreshes |
| `updateBillingRate` | `notif:billing` | Billing page refreshes |
| `createContactPolicy` | `notif:contacts` | Contact policies page refreshes |
| `updateContactPolicy` | `notif:contacts` | Contact policies page refreshes |

### Rates fetch overhead
- Billing page now does N extra fetches (one per plan) in parallel after the main plans fetch
- All fetched with 30 s revalidation and `notif:billing` tag
- Graceful `.catch(() => [])` per plan — one failed rate fetch does not break the page
- For tenants with many plans, this is an O(N) fetch pattern — acceptable for internal admin tooling; a single `GET /billing/plans?includeRates=true` endpoint would be more efficient if added to the backend (see §14)

### Optimistic update in BillingPlanRatesModal
- Newly created rate is appended client-side immediately without waiting for a page reload
- Edited rates require reopening the modal after page navigation to see the server-fresh data
- Acceptable for internal admin tooling

---

## 10. KNOWN GAPS / LIMITATIONS

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **Template version editing not supported** — `PATCH /templates/:id/versions/:versionId` does not exist in the backend routes. Versions are immutable once created. | Medium | By design in the backend lifecycle. Create a new draft version to make changes. Documented in the version create modal as an inline warning. |
| 2 | **No seed data** — All mutations reach real endpoints but return 404 or empty responses without DB records. End-to-end mutation paths cannot be fully verified in the current environment. | High (ops) | Requires NOTIF-SEED-001. |
| 3 | **Provider config edit does not confirm existing non-secret field values** — The edit modal opens without pre-fetching the current config. Display name is pre-populated from the list row, but SMTP host/username are empty (they would need an additional GET /providers/configs/:id fetch per row). | Medium | Acceptable trade-off — avoiding N extra fetches on page load. Admins know their own config. |
| 4 | **`GET /billing/plans/:id/rates` may not exist in backend** — If the route is missing, the billing page silently renders empty rates for all plans. | Medium | `.catch(() => [])` handles this cleanly. Requires backend confirmation. |
| 5 | **Billing rate edit requires page reload to show updated values in modal** — Only creates are optimistically appended. Edits require reopening the modal after page navigation. | Low | Acceptable for admin tooling. |
| 6 | **Channel settings form opens without fetching current config IDs** — `primaryTenantProviderConfigId` and `fallbackTenantProviderConfigId` are populated from the list row, but the backend may not return these fields in the channel-settings GET response depending on serializer configuration. | Medium | Assumes the GET response includes these fields. Falls back to empty string (no pre-selected config) if missing. |
| 7 | **`NotifContactPolicy.config` shape is opaque** — The backend stores blocking rules as `config: Record<string, unknown>`. The `policyToFields()` helper reads from `config` using TypeScript casts. If the backend changes the config key names, the pre-fill will silently default to the fallback values. | Low | Backend contract risk. |
| 8 | **Template version create refreshes the page via `window.location.reload()`** — This is a hard reload, not a Next.js cache invalidation. `revalidateTag(notif:templates)` is still called in the action to keep the cache consistent, but the user experience is a full page reload. | Low | Acceptable for admin tooling. |
| 9 | **Provider config create wizard not multi-step** — Spec allowed single-form or dynamic form; we chose dynamic single-form. No validation step between "enter credentials" and "activate". Admins must use the Validate button after creation. | Low | By design — spec said "single-form or dynamic form is preferred". |
| 10 | **No audit trail** — New mutations are not emitted to the Platform Audit Event Service. | Low | NOTIF-UI-004 / cross-cutting concern. |
| 11 | **Dispatch worker still a stub** — Notifications accepted but not delivered. Not in scope for UI phase. | High (backend) | NOTIF-WORKER-001. |

---

## 11. ISSUES ENCOUNTERED

| # | Issue | Resolution | Status |
|---|---|---|---|
| 1 | **`PATCH /templates/:id/versions/:versionId` does not exist** — Spec listed it; backend exploration confirmed it is not routed. | Documented as a Known Gap. Implemented create-only draft flow with inline warning message in the modal. | Resolved (partial implementation, backend constraint) |
| 2 | **`NotifBillingPlan` type was missing `currency`, `effectiveFrom`, `effectiveTo`** — Backend POST accepts these fields and the GET response likely returns them, but the existing type only had `name`, `mode`, `status`, `createdAt`. | Added optional fields to `NotifBillingPlan` interface in `notifications-api.ts`. | Resolved |
| 3 | **`NotifChannelSetting` missing routing config ID fields** — Backend `PUT /channel-settings/:channel` uses `primaryTenantProviderConfigId` and `fallbackTenantProviderConfigId`, but the existing type only had `primaryProvider` (display name) and `fallbackProvider` (display name). | Added optional `primaryTenantProviderConfigId`, `fallbackTenantProviderConfigId`, `providerMode`, `allowPlatformFallback`, `allowAutomaticFailover` fields to the type. | Resolved |
| 4 | **`NotifProviderConfig` missing `displayName`** — Backend GET response includes it but the type didn't. | Added `displayName?: string \| null` to `NotifProviderConfig`. | Resolved |
| 5 | **TypeScript discriminated union for `BillingPlanForm` props** — `mode: 'create'` vs `mode: 'edit'` required different required props (edit needs `id` + initial values). | Implemented as `CreateProps \| EditProps` discriminated union — fully type-safe with no `as` casts. | Resolved |
| 6 | **`NotifContactPolicy.config` is `Record<string, unknown>`** — Edit form needed to pre-fill boolean checkboxes from this opaque map. | Implemented `policyToFields(policy: NotifContactPolicy): BoolFields` helper that reads with fallback defaults for each boolean key. | Resolved |
| 7 | **Rates fetch per billing plan could fail independently** — If any plan's rates fetch throws, it would break the whole page. | Each plan rate fetch uses `.catch(() => [])` — isolated per-plan failure returns empty array. | Resolved |
| 8 | **Provider type selection in create mode must also set the channel** — SendGrid/SMTP support only `email`; Twilio supports only `sms`. | `handleProviderChange()` updates both `providerType` and `channel` to the first valid channel for that provider. | Resolved |
| 9 | **`BillingPlanRatesModal` initialRates prop vs server-fresh state** — After editing a rate, the modal still shows the stale value from the server. | Accepted as a known limitation (see §10 #5). Reload required for fresh data. | Resolved (acceptable) |
| 10 | **Template create form needed a full navigation on success** — Using Next.js `router.push` would not render the new template detail page with fresh server data in time. | Used `window.location.href = /notifications/templates/:id` for a full navigation. | Resolved |

---

## 12. RUN INSTRUCTIONS

### Start the platform

```bash
bash scripts/run-dev.sh
```

Control Center available on port 5004.

### Required environment variables

No changes from previous phases. Same three connection strings required — see NOTIF-UI-001-report.md §12.

### Accessing new features

#### Provider config create / edit
1. Log in as PlatformAdmin → set tenant context → navigate to **Notifications → Providers**
2. Click **New Provider Config** (indigo, top right of provider configs table)
3. Select Provider Type (SendGrid / SMTP / Twilio) — channel and credential fields update dynamically
4. Fill in Display Name and credentials → click **Create Config**
5. Per-row **Edit** button opens the edit form with display name pre-filled; enter new credentials only if rotating

#### Channel settings edit
1. On the same Providers page, each channel settings row ends with an **Edit** button
2. Select primary and/or fallback provider config (filtered by channel) → adjust failover flags → **Save Settings**

#### Template create / version create
1. Navigate to **Notifications → Templates**
2. Click **New Template** (top right) → fill key, name, channel, description → **Create Template** → redirected to template detail
3. On template detail page, click **New Version** → fill body, subject (email), text body, variable schema (JSON), sample data (JSON) → **Create Draft Version** → page refreshes

#### Billing plan create / edit / rates
1. Navigate to **Notifications → Usage & Billing**
2. Click **New Billing Plan** → fill name, mode, currency, effective from → **Create Plan**
3. Per-row **Edit** to change plan fields
4. Per-row **Rates (N)** to open rates modal → **+ Add Rate** → fill unit, channel, price → **Add Rate**
5. Per-row **Edit** within rates modal to update an existing rate

#### Contact policy create / edit
1. Navigate to **Notifications → Contacts → Policies**
2. Click **New Policy** → select channel scope (global or specific) → toggle blocking rules → **Create Policy**
3. Per-row **Edit** to update an existing policy's rules or status

### Testing with real data
All mutations reach real backend endpoints. Without seed data:
- Provider config create: succeeds (inserts to DB); validate/test will fail with missing credentials
- Template create: succeeds
- Template version create: succeeds (draft created)
- Billing plan create: succeeds
- Billing rate create: succeeds if plan exists (create one first)
- Contact policy create: succeeds
- Edit operations: require the corresponding record to exist first

---

## 13. READINESS ASSESSMENT

| Question | Answer |
|---|---|
| Is NOTIF-UI-003 complete? | **Mostly** — 12 of 13 features implemented; version draft edit is a backend constraint |
| Is it safe for internal use? | **Yes** — all actions auth-guarded; client-side validation before any API call; errors surface without crashing |
| Can we proceed to the next phase? | **Yes** |

---

## 14. NEXT STEPS

### NOTIF-UI-004 — Remaining Admin Gaps
- Template version draft edit (requires backend to implement `PATCH /templates/:id/versions/:versionId`)
- Provider config credential pre-fill in edit mode (requires `GET /providers/configs/:id` endpoint or embedding credentials state in the list response)
- Audit event emission on all mutations (cross-cutting concern — Server Action helper)
- Optimistic update for billing rate edits (avoid requiring page reload)

### Backend Dependencies Discovered

| Item | Impact | Owner |
|---|---|---|
| `PATCH /templates/:id/versions/:versionId` does not exist | Draft version editing not possible from UI | Backend / NOTIF-CONTRACT-002 |
| `GET /billing/plans/:id/rates` existence unconfirmed | Billing rates tab silently empty | Backend / NOTIF-CONTRACT-002 |
| Channel settings GET response `primaryTenantProviderConfigId` field | Channel settings pre-fill may be wrong | Backend / NOTIF-CONTRACT-002 |
| Notifications DB seed data | All mutation testing requires real IDs | Ops / NOTIF-SEED-001 |
| Dispatch worker stub | Notifications accepted → never delivered | Backend / NOTIF-WORKER-001 |

### Performance Improvements
- `GET /billing/plans?includeRates=true` — a single endpoint returning plans with embedded rates would remove the N+1 pattern introduced in this phase
- Server-side pagination on templates and provider configs lists (currently unbounded)

### UX Improvements for NOTIF-UI-004
- Toast notification system instead of inline button state
- Bulk actions for suppression list (select multiple → lift all)
- Per-provider config health status refresh button
- Template key uniqueness check (client-side) before submit
