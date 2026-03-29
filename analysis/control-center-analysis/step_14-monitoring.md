# Step 14 — Monitoring & System Health Dashboard

## Status: Complete — 0 TypeScript errors

---

## Summary

Implemented the **Monitoring & System Health Dashboard** page inside `apps/control-center`.
The page is gated behind `requirePlatformAdmin()` and fetches a single structured response
from `controlCenterServerApi.monitoring.getSummary()`.  All three components — the
health banner, integration table, and alerts panel — are pure server components
(no client-side interactivity needed for a read-only dashboard).

---

## Files Changed

### Modified

| File | Change |
|------|--------|
| `apps/control-center/src/types/control-center.ts` | Replaced old `SystemHealthSummary`; added `MonitoringStatus`, `AlertSeverity`, `IntegrationStatus`, `SystemAlert`, `MonitoringSummary` |
| `apps/control-center/src/lib/control-center-api.ts` | Added new type imports, `MOCK_MONITORING_SUMMARY`, and `monitoring.getSummary()` namespace |

### Created

| File | Purpose |
|------|---------|
| `apps/control-center/src/app/monitoring/page.tsx` | Server component — loads summary, renders shell + three panels |
| `apps/control-center/src/components/monitoring/system-health-card.tsx` | Overall platform status banner; exports reusable `StatusBadge` |
| `apps/control-center/src/components/monitoring/integration-status-table.tsx` | Tabular integration health; sorted Down→Degraded→Healthy |
| `apps/control-center/src/components/monitoring/alerts-panel.tsx` | Alert list sorted Critical→Warning→Info then newest first |

---

## Type Changes

**Old `SystemHealthSummary`** (pre-step-14):
```ts
interface SystemHealthSummary {
  serviceName:  string;
  status:       'ok' | 'degraded' | 'down' | 'unknown';
  version?:     string;
  environment?: string;
  checkedAtUtc: string;
}
```

**New types** (step-14 spec):
```ts
type MonitoringStatus = 'Healthy' | 'Degraded' | 'Down';
type AlertSeverity    = 'Info' | 'Warning' | 'Critical';

interface SystemHealthSummary {
  status:           MonitoringStatus;
  lastCheckedAtUtc: string;
}

interface IntegrationStatus {
  name:             string;
  status:           MonitoringStatus;
  latencyMs?:       number;
  lastCheckedAtUtc: string;
}

interface SystemAlert {
  id:           string;
  message:      string;
  severity:     AlertSeverity;
  createdAtUtc: string;
}

interface MonitoringSummary {
  system:       SystemHealthSummary;
  integrations: IntegrationStatus[];
  alerts:       SystemAlert[];
}
```

---

## API Method

```ts
controlCenterServerApi.monitoring.getSummary(): Promise<MonitoringSummary>
// TODO: replace with GET /platform/monitoring/summary
```

Returns a shallow copy of the mock — safe to mutate without affecting the store.

---

## Mock Data

### System
```
status: 'Healthy'
lastCheckedAtUtc: '2026-03-29T03:50:00Z'
```

### Integrations (5 entries)

| Name | Status | Latency |
|------|--------|---------|
| Identity Service | Healthy | 42 ms |
| Payments Gateway | Degraded | 1 240 ms |
| Notifications | Healthy | 88 ms |
| CareConnect API | Healthy | 115 ms |
| Document Storage | Down | — |

Latency thresholds used for coloring: `> 1 000 ms` → red, `> 400 ms` → amber, `≤ 400 ms` → default.

### Alerts (5 entries)

| ID | Severity | Message |
|----|----------|---------|
| alert-001 | Warning | Payments Gateway latency above 1 000 ms |
| alert-002 | Critical | Document Storage health check failed |
| alert-003 | Info | Scheduled maintenance window 2026-04-01 |
| alert-004 | Info | Identity Service certificate renewal completed |
| alert-005 | Warning | Unusual login volume from GRAYSTONE |

---

## Page Layout (`/monitoring`)

```
/monitoring
├── Header: "System Health" + description + Critical badge (if any)
├── SystemHealthCard        — pulsing dot · overall status · last checked time
├── IntegrationStatusTable  — sorted: Down first, then Degraded, then Healthy
│   ├── Document Storage     [Down]
│   ├── Payments Gateway     [Degraded — 1 240 ms]
│   ├── CareConnect API      [Healthy — 115 ms]
│   ├── Identity Service     [Healthy — 42 ms]
│   └── Notifications        [Healthy — 88 ms]
└── AlertsPanel             — sorted: Critical first, then Warning, then Info
    ├── alert-002  [Critical]   Document Storage unreachable
    ├── alert-001  [Warning]    Payments Gateway latency
    ├── alert-005  [Warning]    GRAYSTONE login volume
    ├── alert-003  [Info]       Maintenance window
    └── alert-004  [Info]       Certificate renewal
```

---

## Component Details

### `SystemHealthCard`
- Pulsing dot animation on `Healthy` status only (CSS `animate-ping`)
- Status-aware background, ring, and text colour via `STATUS_CONFIG` map
- Exports `StatusBadge` reused by `IntegrationStatusTable`

### `IntegrationStatusTable`
- Sorted: `Down → Degraded → Healthy`, then alphabetically within tier
- Header counter: "N / M healthy"
- Latency column coloured by threshold: >1 000 ms = red, >400 ms = amber
- `lastCheckedAtUtc` formatted as relative time (`2m ago`, `3h ago`, etc.)

### `AlertsPanel`
- Sorted: `Critical → Warning → Info`, then newest first within tier
- Left border accent per severity (`border-l-4` red / amber / blue)
- Header shows separate Critical and Warning counts
- Empty state: "No active alerts." message

---

## Conventions Followed

- `requirePlatformAdmin()` guard — identical to all prior steps
- `controlCenterServerApi.*` for all data access — no direct fetch, no import from `apps/web`
- `CCShell userEmail={session.email}` — consistent with steps 5–13
- All components are pure server components — no `'use client'` needed for read-only dashboard
- `MonitoringStatus` and `AlertSeverity` exported as named types for reuse

---

## TypeScript Verification

```
cd apps/control-center && tsc --noEmit
# → 0 errors, 0 warnings
```
