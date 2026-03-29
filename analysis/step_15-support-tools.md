# Step 15 — Support Tools (Internal Case Management)

## Status: Complete — 0 TypeScript errors

---

## Summary

Implemented **Support Tools** inside `apps/control-center` — a full list + detail
workflow for managing internal support cases across tenants.

The list page provides search and filter controls (status, priority). The detail page
shows case metadata, inline status change controls, existing notes, and a new-note
form — all with optimistic UI via a client component + server actions.

---

## Files Changed

### Modified

| File | Change |
|------|--------|
| `apps/control-center/src/types/control-center.ts` | Added `SupportCaseStatus`, `SupportCasePriority`, `SupportCase`, `SupportNote`, `SupportCaseDetail` |
| `apps/control-center/src/lib/control-center-api.ts` | Added type imports, `MOCK_SUPPORT_CASES` store, counter vars, and `support` namespace |

### Created

| File | Purpose |
|------|---------|
| `apps/control-center/src/app/support/page.tsx` | List page — search/filter/paginate support cases |
| `apps/control-center/src/app/support/[id]/page.tsx` | Detail page — shows case; renders interactive panel; 404 on unknown ID |
| `apps/control-center/src/app/support/actions.ts` | Server actions: `updateCaseStatus`, `addCaseNote`, `createSupportCase` |
| `apps/control-center/src/components/support/support-case-table.tsx` | Server component — filterable, paginated table with status/priority badges |
| `apps/control-center/src/components/support/support-detail-panel.tsx` | Client component — optimistic status control + note thread |

---

## Types Added

```ts
type SupportCaseStatus   = 'Open' | 'Investigating' | 'Resolved' | 'Closed';
type SupportCasePriority = 'Low' | 'Medium' | 'High';

interface SupportCase {
  id:           string;
  title:        string;
  tenantId:     string;
  tenantName:   string;
  userId?:      string;
  userName?:    string;
  status:       SupportCaseStatus;
  category:     string;
  priority:     SupportCasePriority;
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface SupportNote {
  id:           string;
  caseId:       string;
  message:      string;
  createdBy:    string;
  createdAtUtc: string;
}

interface SupportCaseDetail extends SupportCase {
  notes: SupportNote[];
}
```

---

## API Methods

```ts
controlCenterServerApi.support.list(params?)
// Params: page, pageSize, search, status, priority
// Returns: Promise<{ items: SupportCase[]; totalCount: number }>
// Sorted: newest updatedAtUtc first

controlCenterServerApi.support.getById(id)
// Returns: Promise<SupportCaseDetail | null>

controlCenterServerApi.support.create(data)
// Returns: Promise<SupportCaseDetail> — status always 'Open'

controlCenterServerApi.support.addNote(caseId, message)
// Returns: Promise<SupportNote> — createdBy: 'admin@legalsynq.com'

controlCenterServerApi.support.updateStatus(caseId, status)
// Returns: Promise<SupportCase>

// TODO: replace with /identity/api/admin/support endpoints
```

---

## Mock Data (7 cases)

| ID | Title | Tenant | Status | Priority | Notes |
|----|-------|--------|--------|----------|-------|
| case-001 | Cannot log in after password reset | Hartwell & Associates | Investigating | High | 2 |
| case-002 | SynqFund report export generates empty file | Meridian Care Partners | Open | Medium | 0 |
| case-003 | GRAYSTONE tenant billing suspended incorrectly | Graystone Gov Solutions | Resolved | High | 2 |
| case-004 | CareConnect map not loading for NEXUSHEALTH users | NexusHealth Network | Open | Medium | 0 |
| case-005 | Request to add second TenantAdmin for THORNFIELD | Thornfield Law Group | Closed | Low | 1 |
| case-006 | Audit log entries missing for HARTWELL org Feb–Mar | Hartwell & Associates | Investigating | High | 1 |
| case-007 | PINNACLE user locked out — MFA device lost | Pinnacle Legal Partners | Resolved | Medium | 1 |

---

## Page Layout

### `/support` (list)
```
Header: "Support Tools" + open/investigating count badges
├── Filter bar: [search input] [status select] [priority select] [Filter btn] [Clear link]
├── Table: Title (→ detail link) · Tenant · Category · Priority · Status · Updated
│   (sorted newest updatedAtUtc first)
└── Pagination + total count
```

### `/support/[id]` (detail)
```
Breadcrumb: Support / Case Title
Header: Case title + Priority badge
Subtitle: Tenant · User · Category · Case ID
└── SupportDetailPanel (client)
    ├── Case Details card   — metadata dl rows
    ├── Update Status card  — status button row (active = current, others clickable)
    └── Internal Notes card — chronological note thread + Add Note textarea + Save button
```

---

## Server Actions

```ts
// apps/control-center/src/app/support/actions.ts
updateCaseStatus(caseId, status)   → UpdateStatusResult
addCaseNote(caseId, message)        → AddNoteResult
createSupportCase(data)             → CreateCaseResult
```

---

## Client Component UX (`SupportDetailPanel`)

| Action | Optimistic | On Error |
|--------|-----------|---------|
| Status change | Immediate button highlight | Revert + inline error |
| Add note | Note appears immediately (dimmed "saving…") | Note removed; text restored; error shown |

---

## Conventions Followed

- `requirePlatformAdmin()` guard on both pages
- `notFound()` on unknown case ID — standard Next.js pattern
- `controlCenterServerApi.*` for all data access — no direct fetch from `apps/web`
- `CCShell userEmail={session.email}` — consistent with steps 5–14
- `Routes.support` used for all hrefs — no hard-coded paths
- Pure server table; interactive detail panel is the only client component

---

## TypeScript Verification

```
cd apps/control-center && tsc --noEmit
# → 0 errors, 0 warnings
```
