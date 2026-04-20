import type { IntegrationStatus, MonitoringStatus } from '@/types/control-center';

interface PublicComponentListProps {
  integrations: IntegrationStatus[];
}

const STATUS_ORDER: Record<MonitoringStatus, number> = { Down: 0, Degraded: 1, Healthy: 2 };

const STATUS_CONFIG: Record<MonitoringStatus, {
  dot:   string;
  badge: string;
  label: string;
}> = {
  Healthy:  { dot: 'bg-green-500',  badge: 'bg-green-50  text-green-700  ring-green-200',  label: 'Operational' },
  Degraded: { dot: 'bg-amber-400',  badge: 'bg-amber-50  text-amber-700  ring-amber-200',  label: 'Degraded'    },
  Down:     { dot: 'bg-red-600',    badge: 'bg-red-50    text-red-700    ring-red-200',    label: 'Outage'      },
};

/**
 * PublicComponentList — simplified, read-only list of monitored service statuses.
 *
 * Designed for the public /status page. Shows only:
 *   - component name (safe display name, not internal ID)
 *   - status (Healthy / Degraded / Down) via an external-friendly label
 *   - last checked timestamp
 *
 * Does NOT expose:
 *   - latency (internal performance metric)
 *   - internal entity IDs
 *   - admin controls
 *   - filter controls
 */
export function PublicComponentList({ integrations }: PublicComponentListProps) {
  if (integrations.length === 0) {
    return (
      <Section title="Components">
        <EmptyState message="No components are currently being monitored." />
      </Section>
    );
  }

  const sorted = [...integrations].sort((a, b) => {
    const diff = STATUS_ORDER[a.status] - STATUS_ORDER[b.status];
    return diff !== 0 ? diff : a.name.localeCompare(b.name);
  });

  return (
    <Section title="Components" subtitle={`${integrations.length} service${integrations.length !== 1 ? 's' : ''} monitored`}>
      <div className="divide-y divide-gray-100">
        {sorted.map(item => (
          <ComponentRow key={item.name} item={item} />
        ))}
      </div>
    </Section>
  );
}

// ── ComponentRow ───────────────────────────────────────────────────────────────

function ComponentRow({ item }: { item: IntegrationStatus }) {
  const cfg     = STATUS_CONFIG[item.status];
  const checked = formatTimestamp(item.lastCheckedAtUtc);

  return (
    <div className="flex items-center gap-3 px-5 py-3.5">
      <span className={`h-2 w-2 rounded-full shrink-0 ${cfg.dot}`} />

      <span className="flex-1 text-sm text-gray-900 truncate min-w-0">
        {item.name}
      </span>

      <span className="text-xs text-gray-400 hidden sm:block shrink-0">
        {checked}
      </span>

      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ring-1 ring-inset shrink-0 ${cfg.badge}`}>
        {cfg.label}
      </span>
    </div>
  );
}

// ── Section wrapper ────────────────────────────────────────────────────────────

function Section({
  title,
  subtitle,
  children,
}: {
  title:     string;
  subtitle?: string;
  children:  React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          {title}
        </h2>
        {subtitle && (
          <p className="text-[11px] text-gray-400 mt-0.5">{subtitle}</p>
        )}
      </div>
      {children}
    </div>
  );
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
    return 'Unavailable';
  }
}
