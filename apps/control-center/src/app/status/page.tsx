import { SystemHealthCard } from '@/components/monitoring/system-health-card';
import { PublicComponentList } from '@/components/monitoring/public-component-list';
import { PublicIncidentsPanel } from '@/components/monitoring/public-incidents-panel';
import type { MonitoringSummary } from '@/types/control-center';
import type { PublicUptimeResponse } from '@/app/api/monitoring/uptime/route';
import { resolveWindow, type SupportedWindow } from '@/lib/uptime-aggregation';

export const dynamic = 'force-dynamic';

async function fetchMonitoringSummary(): Promise<MonitoringSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const res = await fetch(`${base}/api/monitoring/summary`, {
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`Monitoring summary unavailable: ${res.status}`);
  return res.json();
}

async function fetchUptimeHistory(window: SupportedWindow): Promise<PublicUptimeResponse | null> {
  try {
    const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
    const res = await fetch(`${base}/api/monitoring/uptime?window=${window}`, {
      cache: 'no-store',
    });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

/**
 * Public Status Page — /status
 *
 * Publicly accessible (no auth required). Displays real-time platform health
 * sourced from the existing internal summary API. No admin controls, no
 * internal IDs, and no operator-only metadata are exposed.
 *
 * MON-INT-05-001: Supports ?window=24h|7d|30d query param to switch the
 * availability bars to a different historical view. Defaults to 24h.
 *
 * Fields exposed publicly:
 *   - system.status, system.lastCheckedAtUtc (overall health)
 *   - integration.name, integration.status, integration.lastCheckedAtUtc
 *   - alert.severity, alert.message, alert.entityName, alert.createdAtUtc
 *   - uptime bucket: dominantStatus, uptimePercent, insufficientData (no entityId)
 *   - window: selected time window label
 *
 * Fields intentionally excluded:
 *   - alert.id (internal UUID)
 *   - integration.latencyMs (internal performance metric)
 *   - integration.category (not relevant to external users)
 *   - monitored entity UUIDs (stripped by /api/monitoring/uptime BFF layer)
 */
export default async function StatusPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const rawWindow = typeof params.window === 'string' ? params.window : '24h';
  const selectedWindow = resolveWindow(rawWindow);

  let data:       MonitoringSummary | null = null;
  let fetchError: boolean                 = false;

  let uptimeData: PublicUptimeResponse | null = null;

  try {
    [data, uptimeData] = await Promise.all([
      fetchMonitoringSummary(),
      fetchUptimeHistory(selectedWindow),
    ]);
  } catch {
    fetchError = true;
    uptimeData = null;
  }

  const uptimeByName = buildUptimeMap(uptimeData);
  const totalBars    = uptimeData?.totalBars ?? 24;

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">

      {/* Header */}
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-3xl mx-auto px-6 py-3 flex items-center justify-between gap-4">
          <a href="/" className="text-sm font-semibold text-gray-900 hover:text-indigo-700">
            LegalSynq
          </a>
          <nav className="flex items-center gap-4 text-sm">
            <a href="/status" className="text-gray-700 font-medium">
              Status
            </a>
            <a
              href="/login"
              className="inline-flex items-center px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700"
            >
              Sign in
            </a>
          </nav>
        </div>
      </header>

      {/* Main */}
      <main className="max-w-3xl mx-auto px-6 py-10 w-full flex-1">

        {/* Page title + window selector */}
        <div className="mb-8 flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h1 className="text-2xl font-semibold text-gray-900">System Status</h1>
            <p className="text-sm text-gray-500 mt-1">
              Current platform availability and active incidents.
            </p>
          </div>
          <WindowSelector selected={selectedWindow} />
        </div>

        {fetchError ? (
          <StatusUnavailable />
        ) : data ? (
          <div className="space-y-5">
            <SystemHealthCard summary={data.system} />
            <PublicComponentList
              integrations={data.integrations}
              uptimeByName={uptimeByName}
              totalBars={totalBars}
              window={selectedWindow}
            />
            <PublicIncidentsPanel alerts={data.alerts} />
            {data.alerts.filter(a => !a.resolvedAtUtc).length === 0 && (
              <NoActiveIncidents />
            )}
          </div>
        ) : null}

      </main>

      {/* Footer */}
      <footer className="border-t border-gray-200 bg-white">
        <div className="max-w-3xl mx-auto px-6 py-4 flex flex-col sm:flex-row items-center justify-between gap-2 text-xs text-gray-500">
          <span>&copy; {new Date().getFullYear()} LegalSynq. All rights reserved.</span>
          <nav className="flex items-center gap-4">
            <a href="/status" className="hover:text-gray-900">Status</a>
            <a href="/login" className="hover:text-gray-900">Sign in</a>
          </nav>
        </div>
      </footer>

    </div>
  );
}

// ── WindowSelector ────────────────────────────────────────────────────────────

const WINDOW_OPTIONS: { value: SupportedWindow; label: string }[] = [
  { value: '24h', label: '24h' },
  { value: '7d',  label: '7d'  },
  { value: '30d', label: '30d' },
];

function WindowSelector({ selected }: { selected: SupportedWindow }) {
  return (
    <div
      className="inline-flex items-center gap-px rounded-lg border border-gray-200 bg-white overflow-hidden shrink-0"
      role="group"
      aria-label="Select history window"
    >
      {WINDOW_OPTIONS.map(({ value, label }) => {
        const isActive = value === selected;
        return (
          <a
            key={value}
            href={`?window=${value}`}
            aria-pressed={isActive}
            aria-label={`Show ${label} history`}
            className={`px-3 py-1.5 text-xs font-medium transition-colors ${
              isActive
                ? 'bg-indigo-600 text-white'
                : 'text-gray-600 hover:bg-gray-50'
            }`}
          >
            {label}
          </a>
        );
      })}
    </div>
  );
}

// ── Helpers ─────────────────────────────────────────────────────────────────────

import type { PublicUptimeBucket } from '@/app/api/monitoring/uptime/route';

function buildUptimeMap(
  data: PublicUptimeResponse | null,
): Map<string, { uptimePercent: number | null; buckets: PublicUptimeBucket[] }> {
  if (!data || data.components.length === 0) return new Map();
  return new Map(
    data.components.map(c => [c.name, { uptimePercent: c.uptimePercent, buckets: c.buckets }]),
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function StatusUnavailable() {
  return (
    <div className="bg-gray-100 border border-gray-200 rounded-xl px-6 py-8 text-center">
      <div className="flex items-center justify-center gap-2 mb-3">
        <span className="h-3 w-3 rounded-full bg-gray-400 shrink-0" />
        <p className="text-base font-semibold text-gray-600">Status Unavailable</p>
      </div>
      <p className="text-sm text-gray-500 max-w-xs mx-auto">
        We are unable to retrieve the current system status. Please try again shortly.
      </p>
    </div>
  );
}

function NoActiveIncidents() {
  return (
    <div className="bg-white border border-gray-200 rounded-xl px-5 py-5 flex items-center gap-3">
      <span className="h-2 w-2 rounded-full bg-green-500 shrink-0" />
      <p className="text-sm text-gray-600">
        No active incidents.
      </p>
    </div>
  );
}
