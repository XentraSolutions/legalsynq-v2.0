import type { IntegrationStatus } from '@/types/control-center';
import { StatusBadge } from './system-health-card';

interface IntegrationStatusTableProps {
  integrations: IntegrationStatus[];
  title?:       string;
  subtitle?:    string;
}

export function IntegrationStatusTable({ integrations, title, subtitle }: IntegrationStatusTableProps) {
  const ORDER = { Down: 0, Degraded: 1, Healthy: 2 } as const;

  const sorted = [...integrations].sort((a, b) => {
    const diff = ORDER[a.status] - ORDER[b.status];
    return diff !== 0 ? diff : a.name.localeCompare(b.name);
  });

  if (integrations.length === 0) return null;

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            {title ?? 'Integration Health'}
          </h2>
          {subtitle && (
            <p className="text-[11px] text-gray-400 mt-0.5">{subtitle}</p>
          )}
        </div>
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

  const checked = formatTimestamp(item.lastCheckedAtUtc);

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

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
      timeZone: 'UTC',
      timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}
