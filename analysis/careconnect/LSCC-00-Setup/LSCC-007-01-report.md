# LSCC-007-01 — Dashboard Deep-Links & Context Preservation

## Summary

Implemented deep-link context preservation so that navigating from the CareConnect
dashboard (or a filtered referral list) to a referral detail page produces a semantically
correct, label-aware back-button rather than always showing "← Back to Referrals".

---

## Files Changed

| File | Change |
|------|--------|
| `apps/web/src/lib/referral-nav.ts` | New — pure utility module (see below) |
| `apps/web/src/app/(platform)/careconnect/dashboard/page.tsx` | All referral `href` values updated to carry `from=dashboard` |
| `apps/web/src/components/careconnect/referral-quick-actions.tsx` | Added `contextQs` prop; View link uses `buildReferralDetailUrl` |
| `apps/web/src/components/careconnect/referral-list-table.tsx` | Passes `currentQs` as `contextQs` to `ReferralQuickActions` |
| `apps/web/src/app/(platform)/careconnect/referrals/[id]/page.tsx` | Replaced manual `from` check with `resolveReferralDetailBack(searchParams)` |

---

## `referral-nav.ts` Public API

```ts
buildReferralDetailUrl(referralId: string, contextQs: string): string
// Builds /careconnect/referrals/:id?<contextQs>

resolveReferralDetailBack(params: ReferralNavParams): BackTarget
// Returns { href, label } for the detail page back-link

referralNavParamsToQs(params: ReferralNavParams): string
// Serialises nav params to a query string (no leading "?")
```

### Back-link resolution priority

1. **List filters present** (`status`, `search`, `createdFrom`, `createdTo`) →
   back to filtered `/careconnect/referrals?…` with a status-aware label
   (e.g. "← Back to Pending Referrals").
2. **`from=dashboard` only** → back to `/careconnect/dashboard` →
   "← Back to Dashboard".
3. **Fallback** → `/careconnect/referrals` → "← Back to Referrals".

---

## Dashboard Link Inventory (updated)

| Entry point | `href` before | `href` after |
|-------------|--------------|--------------|
| Header "Referral Inbox" button | `/careconnect/referrals` | `…?from=dashboard` |
| StatCard — Active / Pending Referrals | `/careconnect/referrals` | `…?from=dashboard` |
| StatCard — Completed | `…?status=Completed` | `…?from=dashboard&status=Completed` |
| StatCard — Declined | `…?status=Declined` | `…?from=dashboard&status=Declined` |
| StatCard — Accepted (receiver) | `…?status=Accepted` | `…?from=dashboard&status=Accepted` |
| Referral Activity — Total Referrals KPI | static div | `StatCard` linking `…?from=dashboard&createdFrom=…&createdTo=…` |
| Referral Activity — Pending KPI | static div | `StatCard` with `status=New&createdFrom=…` |
| Referral Activity — Accepted KPI | static div | `StatCard` with `status=Accepted&createdFrom=…` |
| SectionCard "Active Referrals" viewAll | `/careconnect/referrals` | `…?from=dashboard` |
| SectionCard "Pending Referrals" viewAll | `…?status=New` | `…?from=dashboard&status=New` |
| QuickAction "All Referrals" | `/careconnect/referrals` | `…?from=dashboard` |
| QuickAction "Referral Inbox" | `/careconnect/referrals` | `…?from=dashboard` |

---

## Context Flow

```
Dashboard link (from=dashboard)
  ↓
/careconnect/referrals?from=dashboard[&status=…]   ← list page
  ↓  (ReferralListTable passes currentQs to ReferralQuickActions)
  ↓  (ReferralQuickActions View link = buildReferralDetailUrl(id, currentQs))
/careconnect/referrals/:id?from=dashboard[&status=…]  ← detail page
  ↓  resolveReferralDetailBack({ from, status, … })
"← Back to Dashboard"  or  "← Back to Pending Referrals"  etc.
```

---

## Design Notes

- All utility logic is pure functions in `referral-nav.ts` — zero React / Next.js
  imports, fully unit-testable without a browser.
- The `contextQs` prop on `ReferralQuickActions` defaults to `''` so all existing
  call-sites that don't pass it continue to work unchanged.
- The Acceptance Rate KPI card remains a non-clickable `<div>` because it is a
  computed ratio that does not map to a filterable list endpoint.
