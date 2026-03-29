import type { IntegrationStatus } from '@/types/control-center';
import { StatusBadge } from './system-health-card';

interface IntegrationStatusTableProps {
  integrations: IntegrationStatus[];
}

/**
 * IntegrationStatusTable — tabular view of downstream service health.
 *
 * Pure server component. Renders name, status badge, latency, and
 * last-checked time for each integration.
 *
 * Sorted: Down → Degraded → Healthy, then alphabetically within each group.
 */
export function IntegrationStatusTable({ integrations }: IntegrationStatusTableProps) {
  const ORDER = { Down: 0, Degraded: 1, Healthy: 2 } as const;

  const sorted = [...integrations].sort((a, b) => {
    const diff = ORDER[a.status] - ORDER[b.status];
    return diff !== 0 ? diff : a.name.localeCompare(b.name);
  });

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Integration Health
        </h2>
        <span className="text-xs text-gray-400 tabular-nums">
          {integrations.filter(i => i.status === 'Healthy').length} / {integrations.length} healthy
        </span>
      </div>

      <div className="divide-y divide-gray-100">
        {sorted.map(item => (
          <IntegrationRow key={item.name} item={item} />
        ))}
      </div>
    </div>
  );
}

// ── IntegrationRow ────────────────────────────────────────────────────────────

function IntegrationRow({ item }: { item: IntegrationStatus }) {
  const latency  = item.latencyMs !== undefined ? `${item.latencyMs} ms` : '—';
  const latencyColor =
    item.latencyMs === undefined  ? 'text-gray-400' :
    item.latencyMs > 1000         ? 'text-red-600 font-semibold' :
    item.latencyMs > 400          ? 'text-amber-600 font-semibold' :
                                    'text-gray-700';

  const checked = formatRelative(item.lastCheckedAtUtc);

  return (
    <div className="flex items-center gap-3 px-5 py-3.5">

      {/* Status dot */}
      <StatusDot status={item.status} />

      {/* Name */}
      <span className="flex-1 text-sm font-medium text-gray-900 truncate">
        {item.name}
      </span>

      {/* Latency */}
      <span className={`text-xs tabular-nums w-20 text-right ${latencyColor}`}>
        {latency}
      </span>

      {/* Last checked */}
      <span className="text-xs text-gray-400 w-24 text-right hidden sm:block">
        {checked}
      </span>

      {/* Badge */}
      <div className="w-24 flex justify-end">
        <StatusBadge status={item.status} />
      </div>
    </div>
  );
}

function StatusDot({ status }: { status: IntegrationStatus['status'] }) {
  const colors = {
    Healthy:  'bg-green-500',
    Degraded: 'bg-amber-400',
    Down:     'bg-red-600',
  };
  return (
    <span className={`h-2 w-2 rounded-full shrink-0 ${colors[status]}`} />
  );
}

// ── helpers ───────────────────────────────────────────────────────────────────

function formatRelative(iso: string): string {
  try {
    const diffMs  = Date.now() - new Date(iso).getTime();
    const minutes = Math.floor(diffMs / 60_000);
    if (minutes < 1)  return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24)   return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  } catch {
    return iso;
  }
}
