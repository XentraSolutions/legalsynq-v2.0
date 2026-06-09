# INV-TPL-05 — Standalone Invoice Template Admin UI

## 1. Goal & scope

Build a **standalone** Next.js admin app, `artifacts/tenant-billing-admin`,
that lets an operator manage Tenant Billing invoice templates and preview
rendered invoice output against the standalone `services/tenant-billing-api`
.NET service. Mirror the proven `artifacts/commerce-admin` pattern (Next.js
14 App Router + Tailwind + Server Actions). Explicitly **out of scope** for
this ticket: any LegalSynq / Tenant-Portal integration, any auth/JWT, any
file upload, any PDF/email rendering paths.

## 2. What was built

A new artifact `tenant-billing-admin` (port `23869`, base path
`/tenant-billing-admin`) with three screens:

- **`/invoice-templates`** — list templates with scope toggle
  (`?scope=tenant|platform`), inline lifecycle actions, status / accent /
  default-due-days at a glance.
- **`/invoice-templates/new` and `/invoice-templates/[id]`** — single
  rich form covering Template, Branding, Issuer/From, and Defaults
  sections; create + update via server actions.
- **`/invoice-preview`** — ad-hoc preview pane: tenant id + invoice id
  inputs → JSON summary panel + sandboxed iframe of the rendered HTML.

A persistent "Standalone tenant context" bar sits above the main pane on
every page; the active tenant id is stored in a cookie (`tba.tenantId`)
and re-attached to every tenant-scoped API call as `X-Tenant-Id`.

## 3. Architecture & request flow

The browser inside the Replit preview iframe **cannot** reach the .NET
service on `localhost:5001` directly. So all API I/O happens on the Next
server:

```
Browser ──► Next page (server component)  ──► Tenant Billing API (5001)
        ──► Next server action            ──► Tenant Billing API (5001)
```

- **Reads** happen in server components that call `templateApi.ts`
  (which wraps `lib/api.ts`). Lists, single fetches, and the preview
  hydration all use this path.
- **Writes** happen in `"use server"` actions:
  - `createTemplateAction` / `updateTemplateAction` (in
    `app/invoice-templates/templateActions.ts`)
  - `activateAction` / `retireAction` / `makeDefaultAction` (in
    `app/invoice-templates/lifecycleActions.ts`)
  - `setTenantContextAction` for the cookie
  - `previewInvoiceAction` for the preview screen

`lib/api.ts` exposes a `request<T>` wrapper that:
- Reads the base URL from `NEXT_PUBLIC_TENANT_BILLING_API_BASE_URL`
  (resolved on the server too).
- Conditionally adds `X-Tenant-Id` only when an opts.tenantId is passed
  (platform routes never receive the header).
- Throws `ApiError(status, message, body)` on non-2xx so server actions
  can surface a useful message and field-level detail to the form.
- Adds `cache: "no-store"` so admin views are always live.

## 4. File layout

```
artifacts/tenant-billing-admin/
├── .replit-artifact/artifact.toml      # PORT=23869, BASE_PATH=/tenant-billing-admin
├── package.json                        # Next 14, React 18, Tailwind 3.4, lucide
├── next.config.mjs                     # basePath + assetPrefix from env
├── tailwind.config.ts                  # purple #7c3aed accent
├── tsconfig.json                       # bundler resolution, @/* alias
├── styles/globals.css
├── app/
│   ├── layout.tsx                      # SideNav + TenantContextBar shell
│   ├── page.tsx                        # redirects → /invoice-templates
│   ├── TenantContextBar.tsx            # client component
│   ├── tenantContextActions.ts         # server action (cookie set/clear)
│   ├── invoice-templates/
│   │   ├── page.tsx                    # list (server component)
│   │   ├── ScopeToggle.tsx
│   │   ├── TemplateListClient.tsx
│   │   ├── RowActions.tsx              # client → lifecycle actions
│   │   ├── TemplateForm.tsx            # client form
│   │   ├── templateApi.ts              # server-side API helpers
│   │   ├── templateActions.ts          # create / update server actions
│   │   ├── lifecycleActions.ts         # activate / retire / default
│   │   ├── new/page.tsx
│   │   └── [id]/page.tsx
│   └── invoice-preview/
│       ├── page.tsx
│       ├── PreviewClient.tsx
│       └── previewAction.ts
├── components/                         # SideNav, PageHeader, DataTable,
│                                       # StatusBadge, EmptyState,
│                                       # ErrorBox, SuccessBox
└── lib/                                # api, types, enums, format,
                                        # tenantContext, validation
```

## 5. Tenant context handling

Tenant-scoped routes require `X-Tenant-Id`; platform routes do not. We
keep the active tenant in a single cookie:

- **Cookie**: `tba.tenantId`, 30-day max-age, `SameSite=Lax`, not
  `httpOnly` (the value is non-secret).
- **Validation**: a strict GUID regex in `lib/tenantContext.ts` is
  applied both when reading and when writing — invalid values are
  rejected before the cookie is set, and an invalid cookie is treated
  as "no tenant" rather than fired off to the API.
- **UX**: the `TenantContextBar` always shows the current tenant
  context (or "none — required for tenant routes"). Changing the value
  triggers `revalidatePath("/", "layout")` so every server component
  re-runs against the new tenant.
- **Read paths**: tenant pages call `getTenantId()` and short-circuit
  to a friendly empty state if missing rather than 400-bombing.

## 6. List page behaviour

- Default scope is `tenant`. The scope is read from `?scope=` and
  validated by `isTenantScope`.
- Server component fetches the list via `listTemplates`.
- The client wrapper renders a `DataTable` with columns: name (with
  default badge), status badge, accent swatch + hex, **issuer display
  name**, default due days, updated-at, and a per-row action cluster.
- Because the API summary DTO does not include issuer fields, the page
  enriches each row server-side via `listEnrichedTemplates`, which fans
  out one `getTemplate` call per row in parallel after the list call
  (`Promise.all`) and degrades gracefully to `null` on per-row failure.
- Row actions (Edit, Activate, Retire, Make default) call server
  actions and surface errors above the table; the page is then
  revalidated. Activate is hidden once Active; Make default is hidden
  for retired templates and the current default.

## 7. Editor behaviour

- Single client form covers all four sections of the DTO. The same
  component is used for both create and edit; mode-specific affordances
  are tucked behind the `mode` prop:
  - Create has an "Initial status" select (Draft/Active) and an
    "Is default" checkbox.
  - Edit hides those (lifecycle is managed from the list) and
    displays the current status / default badge in the header.
- Retired templates are read-only; the submit button is disabled and a
  red banner explains why.
- Client-side validation runs before the server action and mirrors the
  backend's permissive checks: name required, accent color `#RRGGBB`,
  default due days `0..365`, issuer email shape, issuer website
  `http(s)://`. Per-field errors display under each input; the server
  remains the source of truth and any 400 from the API is surfaced
  verbatim, including `detail` from RFC 7807 problem responses.
- Empty inputs are normalised to `null` before posting so the API gets
  `null` (not empty string) for optional fields.
- Success path: create redirects to the edit page with a success
  flash; update issues `router.refresh()` plus a green box.

## 8. Lifecycle actions

The three lifecycle endpoints are wrapped in dedicated server actions
in `lifecycleActions.ts`. Each takes `(scope, id)` from the row, looks
up the cookie tenant id when scope is `tenant`, and returns
`{ ok, error }`. The list page is revalidated after each successful
call so the table reflects the new status.

## 9. Invoice preview

`/invoice-preview` is a one-shot ad-hoc inspector:

- The form takes both a tenant GUID and an invoice GUID. The tenant id
  is prefilled from the cookie context.
- Submitting calls `previewInvoiceAction`, which fetches both
  `/api/invoices/{id}/render` (JSON) and
  `/api/invoices/{id}/render/html` (text) in sequence with the
  `X-Tenant-Id` header.
- Errors map to helpful messages (404 → "Invoice not found for tenant
  …"; everything else surfaces the API's `title` + `detail`).
- The result panel renders:
  - A condensed JSON summary (invoice number, status, currency, dates,
    amounts, customer, template, accent) plus a collapsible raw JSON
    block for everything else.
  - The HTML in an `<iframe sandbox="">` via `srcDoc=` — keeps the
    document fully isolated from the admin UI.
- A yellow "no template snapshot" warning is surfaced when the JSON
  document has no template name / issuer / accent fields, so operators
  understand why the HTML may look unbranded.

## 10. Differences from `commerce-admin`

- Adds the **Standalone tenant context bar** and cookie-backed
  `X-Tenant-Id` plumbing (commerce-admin has no tenant scoping).
- Splits server actions per-resource (`templateActions`,
  `lifecycleActions`, `previewAction`) instead of co-locating with
  pages, because this app has a richer mutation surface.
- Uses an iframe with `sandbox=""` for HTML preview (commerce-admin
  has no equivalent rendering screen).
- Otherwise identical: Next.js 14, Tailwind, lucide-react, custom
  components, server-component reads, server-action writes, no
  shared UI kit.

## 11. Verification

- `pnpm install` (workspace root) — clean install.
- `pnpm --filter @workspace/tenant-billing-admin run typecheck` — passes.
- Workflow `artifacts/tenant-billing-admin: web` starts in ~1.4 s,
  routes return:
  - `GET /tenant-billing-admin/invoice-templates` → 200
  - `GET /tenant-billing-admin/invoice-templates?scope=platform` → 200
  - `GET /tenant-billing-admin/invoice-preview` → 200
- End-to-end test executed via the testing harness covered:
  redirect → list → scope toggle → empty states → create with
  validation error and successful create → edit + save → tenant
  context set → tenant-scope page after context → preview page input
  validation. All steps passed.

## 12. Limitations & follow-ups

- The Tenant Billing API is in-memory; refreshing the workflow wipes
  all data. The admin UI surfaces this honestly (empty state, no
  fake loading shimmers).
- No multi-select / bulk operations, no search/filter, no pagination
  (matches commerce-admin).
- No optimistic updates; every mutation is a full round-trip + revalidate.
- `templateApi` calls always set `cache: "no-store"`. If the table grows
  large we could re-enable revalidation tags instead.
- No Storybook / Vitest / Playwright in the artifact itself — relies on
  workspace-level harnesses for verification.

## 13. How to use it

1. Open the Replit preview and pick **Tenant Billing Admin** in the
   artifact dropdown — or hit `/tenant-billing-admin/` directly.
2. To work with **platform** templates: click the *Platform templates*
   tab; no tenant id needed.
3. To work with **tenant** templates: click *Set tenant id* in the top
   bar, paste a GUID, save; the tenant tab and preview screen will then
   send that id as `X-Tenant-Id` for every call.
4. Create / edit / activate / retire / make-default from the list page.
5. To preview an invoice, go to **Invoice Preview**, paste the tenant
   and invoice GUIDs, and load.
