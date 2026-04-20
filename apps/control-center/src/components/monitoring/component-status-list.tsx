'use client';

import { useState } from 'react';
import type { IntegrationStatus, MonitoringStatus } from '@/types/control-center';
import { StatusBadge } from './system-health-card';

interface ComponentStatusListProps {
  integrations: IntegrationStatus[];
}

type FilterValue = 'All' | MonitoringStatus;

const FILTERS: FilterValue[] = ['All', 'Healthy', 'Degraded', 'Down'];

const STATUS_ORDER: Record<MonitoringStatus, number> = { Down: 0, Degraded: 1, Healthy: 2 };

const CATEGORY_LABELS: Record<string, string> = {
  infrastructure: 'Infra',
  product:        'Product',
};

/**
 * ComponentStatusList — unified, filterable list of all monitored entities.
 *
 * Replaces the two split IntegrationStatusTable cards (Platform Services / Products).
 * Client component: filter state is local; no additional API calls are made.
 * Data comes directly from MonitoringSummary.integrations — no status recomputation.
 */
export function ComponentStatusList({ integrations }: ComponentStatusListProps) {
  const [activeFilter, setActiveFilter] = useState<FilterValue>('All');

  const sorted = [...integrations].sort((a, b) => {
    const diff = STATUS_ORDER[a.status] - STATUS_ORDER[b.status];
    return diff !== 0 ? diff : a.name.localeCompare(b.name);
  });

  const visible =
    activeFilter === 'All'
      ? sorted
      : sorted.filter(i => i.status === activeFilter);

  const hasCategory = integrations.some(i => i.category);

  // Per-filter counts for the filter buttons
  const counts: Record<FilterValue, number> = {
    All:      integrations.length,
    Healthy:  integrations.filter(i => i.status === 'Healthy').length,
    Degraded: integrations.filter(i => i.status === 'Degraded').length,
    Down:     integrations.filter(i => i.status === 'Down').length,
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

      {/* Header */}
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex flex-wrap items-center gap-3">
        <div className="flex-1 min-w-0">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            All Components
          </h2>
          <p className="text-[11px] text-gray-400 mt-0.5">
            {counts.Healthy} / {counts.All} healthy
            {activeFilter !== 'All' && ` · showing ${visible.length} ${activeFilter.toLowerCase()}`}
          </p>
        </div>

        {/* Filter buttons */}
        <div className="flex items-center gap-1.5 flex-wrap shrink-0" role="group" aria-label="Filter by status">
          {FILTERS.map(f => (
            <FilterButton
              key={f}
              label={f}
              count={counts[f]}
              active={activeFilter === f}
              onClick={() => setActiveFilter(f)}
            />
          ))}
        </div>
      </div>

      {/* Rows */}
      {integrations.length === 0 ? (
        <EmptyState message="No monitored components." />
      ) : visible.length === 0 ? (
        <EmptyState message={`No ${activeFilter.toLowerCase()} components.`} />
      ) : (
        <div className="divide-y divide-gray-100">
          {visible.map(item => (
            <ComponentRow key={item.name} item={item} showCategory={hasCategory} />
          ))}
        </div>
      )}
    </div>
  );
}

// ── ComponentRow ───────────────────────────────────────────────────────────────

function ComponentRow({
  item,
  showCategory,
}: {
  item:         IntegrationStatus;
  showCategory: boolean;
}) {
  const latency = item.latencyMs !== undefined ? `${item.latencyMs} ms` : '—';
  const latencyColor =
    item.latencyMs === undefined ? 'text-gray-400'             :
    item.latencyMs > 1000        ? 'text-red-600 font-semibold' :
    item.latencyMs > 400         ? 'text-amber-600 font-semibold' :
                                   'text-gray-700';

  const checked  = formatTimestamp(item.lastCheckedAtUtc);
  const catLabel = item.category ? (CATEGORY_LABELS[item.category] ?? item.category) : null;

  return (
    <div className="flex items-center gap-3 px-5 py-3.5">

      {/* Status dot */}
      <StatusDot status={item.status} />

      {/* Name */}
      <span className="flex-1 text-sm font-medium text-gray-900 truncate min-w-0">
        {item.name}
      </span>

      {/* Category chip (only if any row has a category) */}
      {showCategory && (
        <span className="text-[10px] font-medium uppercase tracking-wide text-gray-400 w-14 text-right hidden md:block shrink-0">
          {catLabel ?? '—'}
        </span>
      )}

      {/* Latency */}
      <span className={`text-xs tabular-nums w-20 text-right shrink-0 ${latencyColor}`}>
        {latency}
      </span>

      {/* Last checked */}
      <span className="text-xs text-gray-400 w-24 text-right hidden sm:block shrink-0">
        {checked}
      </span>

      {/* Badge */}
      <div className="w-20 flex justify-end shrink-0">
        <StatusBadge status={item.status} />
      </div>
    </div>
  );
}

// ── FilterButton ───────────────────────────────────────────────────────────────

function FilterButton({
  label,
  count,
  active,
  onClick,
}: {
  label:   FilterValue;
  count:   number;
  active:  boolean;
  onClick: () => void;
}) {
  const activeStyles: Record<FilterValue, string> = {
    All:      'bg-gray-700  text-white  border-gray-700',
    Healthy:  'bg-green-600 text-white  border-green-600',
    Degraded: 'bg-amber-500 text-white  border-amber-500',
    Down:     'bg-red-600   text-white  border-red-600',
  };
  const idleStyles: Record<FilterValue, string> = {
    All:      'bg-white text-gray-600 border-gray-200 hover:bg-gray-50',
    Healthy:  'bg-white text-green-700 border-gray-200 hover:bg-green-50',
    Degraded: 'bg-white text-amber-700 border-gray-200 hover:bg-amber-50',
    Down:     'bg-white text-red-700   border-gray-200 hover:bg-red-50',
  };

  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${active ? activeStyles[label] : idleStyles[label]}`}
    >
      {label}
      <span className={`tabular-nums ${active ? 'opacity-80' : 'opacity-60'}`}>
        {count}
      </span>
    </button>
  );
}

// ── StatusDot ──────────────────────────────────────────────────────────────────

function StatusDot({ status }: { status: MonitoringStatus }) {
  const colors: Record<MonitoringStatus, string> = {
    Healthy:  'bg-green-500',
    Degraded: 'bg-amber-400',
    Down:     'bg-red-600',
  };
  return <span className={`h-2 w-2 rounded-full shrink-0 ${colors[status]}`} />;
}

// ── EmptyState ─────────────────────────────────────────────────────────────────

function EmptyState({ message }: { message: string }) {
  return (
    <div className="px-5 py-8 text-center">
      <p className="text-sm text-gray-400">{message}</p>
    </div>
  );
}

// ── helpers ────────────────────────────────────────────────────────────────────

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour:         '2-digit',
      minute:       '2-digit',
      second:       '2-digit',
      hour12:       false,
      timeZone:     'UTC',
      timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}
