# Step 28 — SynqAudit UI (Control Center)

**Date:** 2026-03-31  
**Scope:** Dedicated audit investigation section in the Control Center (Next.js 14, port 5004)  
**Status:** ✅ Complete — both Next.js apps `✓ Ready`, 0 compile errors

---

## Objective

Build a fully functional, production-quality audit investigation UI inside the Control Center, surfacing the Platform Audit Event Service (port 5007) to platform administrators. The UI must cover: live event investigation, correlation ID tracing, async export jobs, HMAC-SHA256 integrity checkpoints, and legal hold management.

---

## Architecture Overview

```
Control Center (Next.js 14 — port 5004)
│
├── /synqaudit                        ← Overview + quick-nav
├── /synqaudit/investigation          ← Paged event stream with filter bar
├── /synqaudit/trace                  ← Correlation ID trace timeline
├── /synqaudit/exports                ← Async export job submission
├── /synqaudit/integrity              ← HMAC-SHA256 checkpoint management
└── /synqaudit/legal-holds            ← Legal hold CRUD per audit record
│
├── components/synqaudit/
│   ├── synqaudit-badges.tsx          ← Server-safe badge components + formatters
│   ├── investigation-workspace.tsx   ← Interactive client workspace
│   ├── trace-timeline.tsx            ← Correlation trace client component
│   ├── export-request-form.tsx       ← Export job client form
│   ├── integrity-panel.tsx           ← Checkpoint list + generate form
│   └── legal-hold-manager.tsx        ← Hold CRUD client component
│
└── app/api/synqaudit/                ← Server-side mutation proxies
    ├── exports/route.ts
    ├── integrity/generate/route.ts
    └── legal-holds/[id]/
        ├── route.ts                  ← Create hold
        └── release/route.ts          ← Release hold
```

**Pattern:** Server Components fetch data and pass it to Client Components for interactivity. Client Components call Next.js API routes for mutations; those routes proxy to `controlCenterServerApi` (which talks to the audit service via the gateway).

---

## Files Created

### Pages (6)

| File | Route | Server Fetch |
|---|---|---|
| `app/synqaudit/page.tsx` | `/synqaudit` | `auditCanonical.list({ page:1, pageSize:20 })` |
| `app/synqaudit/investigation/page.tsx` | `/synqaudit/investigation` | `auditCanonical.list(fullParams)` with all 9 filter params |
| `app/synqaudit/trace/page.tsx` | `/synqaudit/trace` | `auditCanonical.list({ correlationId, pageSize:500 })` sorted chronologically |
| `app/synqaudit/exports/page.tsx` | `/synqaudit/exports` | No fetch (form-only page) |
| `app/synqaudit/integrity/page.tsx` | `/synqaudit/integrity` | `auditIntegrity.list()` |
| `app/synqaudit/legal-holds/page.tsx` | `/synqaudit/legal-holds` | `auditLegalHolds.listForRecord(auditId)` (conditional on query param) |

All pages call `requirePlatformAdmin()` as the first line.

### Client Components (6)

#### `synqaudit-badges.tsx`
No `'use client'` directive — server-safe. Exports:
- `SeverityBadge` — colour-coded: info=blue, warn=amber, error=red, critical=dark-red
- `CategoryBadge` — colour-coded: security=red, access=indigo, business=purple, compliance=green, etc.
- `OutcomeBadge` — success=green, failure=red, partial=amber, pending=gray
- `formatUtc(ts)` — compact `DD MMM YY HH:mm` format
- `formatUtcFull(ts)` — full ISO-style `YYYY-MM-DD HH:mm:ss` format

#### `investigation-workspace.tsx`
Interactive event investigation workspace. Features:
- **Filter bar** — 9 controlled inputs: keyword search, event type, category, severity, actor ID, correlation ID, date from/to, target type
- **Active filter chips** — visual summary of applied filters below the bar
- **Apply / Clear** — `router.push()` via `useTransition` for smooth URL-driven filter state
- **Event stream table** — columns: time (UTC), severity, event type, category, actor (name + ID), target, outcome, correlation ID (truncated)
- **Row click → detail panel** — full `EventDetailPanel` side panel showing all fields: classification, source, timing, actor, target, scope, tracing (IDs + hash), description, tags, before/after JSON, metadata JSON, link to trace
- **Pagination** — ellipsis-aware page range builder, URL-preserving `PagerLink`s

#### `trace-timeline.tsx`
Correlation trace viewer. Features:
- Search form — enter correlation ID and submit to navigate to `?correlationId=…`
- Vertical timeline — dot (severity-coloured) + card per event, chronological
- First event has indigo dot + border to highlight request origin
- Card click → detail panel (same pattern as investigation workspace)
- `serviceveityDotColor()` maps severity → Tailwind dot class

#### `export-request-form.tsx`
Async export job form. Features:
- Format radio buttons: JSON, CSV, NDJSON
- Filter inputs: tenant ID, event type, category, severity, correlation ID, date from/to
- Checkboxes: include before/after state snapshots, include tags
- `POST /api/synqaudit/exports` on submit
- `ExportJobCard` result component: shows status badge, record count, download link (when `Completed`), polling guidance (when `Pending`/`Processing`)

#### `integrity-panel.tsx`
HMAC checkpoint management. Features:
- Generate form: checkpoint type, from/to date range, `POST /api/synqaudit/integrity/generate`
- `CheckpointRow` — collapsible with: checkpoint ID, aggregate hash (truncated), record count, from/to window, created timestamp, validity indicator (✓ valid / ✗ invalid)
- Newly generated checkpoint highlighted in green and auto-expanded

#### `legal-hold-manager.tsx`
Legal hold CRUD. Features:
- Active holds list with release button (`POST /api/synqaudit/legal-holds/[id]/release`)
- Create form: legal authority (required), notes (optional); `POST /api/synqaudit/legal-holds/[id]`
- Optimistic updates — hold list updates in-place via `useState` without page reload
- Released holds shown in dimmed section below

### API Route Handlers (4)

All handlers follow the same pattern:
1. `requirePlatformAdmin()` — throws/redirects if not authenticated as platform admin
2. Parse and validate the request body
3. Call `controlCenterServerApi.*()` — server-side, has access to the service token
4. Return `NextResponse.json(result, { status: 201 | 200 })`
5. Catch and return `{ message }` with 500 on upstream failure

**Dynamic segment naming:** Both `/legal-holds/[id]` (create) and `/legal-holds/[id]/release` (release) use the same `[id]` segment name. This satisfies Next.js's constraint that sibling dynamic path segments must share the same slug name. The actual semantic (`auditId` vs `holdId`) is determined by context, not the param name.

### Extended Type Definitions (pre-existing, confirmed)

`CanonicalAuditEvent` was extended in a prior session with:
```typescript
action?:        string;
before?:        string;       // JSON string
after?:         string;       // JSON string
tags?:          string[];
sourceService?: string;
actorType?:     string;
requestId?:     string;
sessionId?:     string;
hash?:          string;       // HMAC-SHA256 of record
```

New types confirmed in `types/control-center.ts`:
- `AuditExport` — exportId, format, status, recordCount, downloadUrl, createdAtUtc, completedAtUtc, errorMessage
- `AuditExportFormat` — `'Json' | 'Csv' | 'Ndjson'`
- `IntegrityCheckpoint` — checkpointId, checkpointType, aggregateHash, recordCount, fromRecordedAtUtc, toRecordedAtUtc, createdAtUtc, isValid?
- `LegalHold` — holdId, auditId, legalAuthority, notes?, heldAtUtc, heldByUserId?, releasedAtUtc?, isActive

---

## Issues Encountered & Resolved

### Next.js Dynamic Route Conflict

**Error:**
```
Error: You cannot use different slug names for the same dynamic path ('auditId' !== 'holdId').
```

**Cause:** Two route files were initially created as:
- `legal-holds/[auditId]/route.ts`
- `legal-holds/[holdId]/release/route.ts`

Next.js requires that all dynamic segments at the same path depth share the same name.

**Fix:** Renamed both directories to `[id]`. Updated both handlers to use `params.id`:
- `[id]/route.ts` → extracts `auditId = params.id`
- `[id]/release/route.ts` → extracts `holdId = params.id`

Client-side fetch URLs are unchanged — the actual UUIDs are passed correctly in the URL path.

---

## Navigation

`lib/nav.ts` — SYNQAUDIT section (added in prior session, confirmed present):
```typescript
{
  label: 'SYNQAUDIT',
  items: [
    { label: 'Overview',      href: '/synqaudit',             icon: 'ri-shield-check-line' },
    { label: 'Investigation', href: '/synqaudit/investigation', icon: 'ri-search-eye-line' },
    { label: 'Trace Viewer',  href: '/synqaudit/trace',        icon: 'ri-git-branch-line' },
    { label: 'Exports',       href: '/synqaudit/exports',      icon: 'ri-download-cloud-line' },
    { label: 'Integrity',     href: '/synqaudit/integrity',    icon: 'ri-fingerprint-line' },
    { label: 'Legal Holds',   href: '/synqaudit/legal-holds',  icon: 'ri-scales-3-line' },
  ],
}
```

---

## HIPAA Alignment Notes

| SynqAudit Feature | HIPAA §164.312 Relevance |
|---|---|
| Canonical event stream (`investigation`) | §164.312(b) — Audit Controls: hardware, software, and procedural mechanisms to record/examine activity |
| Correlation trace viewer | §164.308(a)(1)(ii)(D) — Information System Activity Review |
| Legal holds | §164.316(b)(1) — Documentation retention; supports litigation hold requirements |
| HMAC-SHA256 integrity checkpoints | §164.312(c)(1) — Integrity: protect against improper alteration or destruction |
| Export jobs | §164.308(a)(1)(ii)(D) — enables external compliance review / e-discovery exports |
| `requirePlatformAdmin()` on all routes | §164.312(a)(1) — Access Control: only authorized persons access audit records |

---

## Build Verification

```
[control-center] ✓ Ready in 3.3s
[web]            ✓ Ready in 3.5s
[control-center] 0 Error(s)
```

No TypeScript compile errors. No Next.js routing errors. Auth guard correctly intercepts unauthenticated requests to `/synqaudit/*` and redirects to login.

---

## Test Coverage

No new unit tests — the SynqAudit UI is UI/integration territory. Existing 95/95 backend tests continue to pass (no .NET changes in this step).

Manual smoke-test checklist (requires platform admin session):
- [ ] `/synqaudit` — overview renders stat cards and quick-nav
- [ ] `/synqaudit/investigation` — filter bar applies filters via URL, table renders events, detail panel opens on row click
- [ ] `/synqaudit/trace?correlationId=<id>` — timeline renders for valid correlation ID
- [ ] `/synqaudit/exports` — form submits, ExportJobCard renders
- [ ] `/synqaudit/integrity` — checkpoint list renders, generate form works
- [ ] `/synqaudit/legal-holds?auditId=<id>` — holds render, create/release work

---

## Next Steps (Post-Step-28)

- **Step 29 (potential):** Add `GET /api/synqaudit/exports/[id]` polling route + auto-polling in `ExportJobCard` (currently manual)
- **Step 29 (potential):** Integrity checkpoint auto-verify — compare stored hash against live recompute
- **Step 29 (potential):** `apps/web` tenant portal activity page — scope canonical events by `tenantId` (Phase 2 of T005)
- **Step 29 (potential):** Real-time audit feed using SSE or WebSocket from the audit service
