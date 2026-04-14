import type { SystemAlert, AlertSeverity } from '@/types/control-center';

interface AlertsPanelProps {
  alerts: SystemAlert[];
}

const SEVERITY_CONFIG: Record<AlertSeverity, {
  bg:     string;
  border: string;
  icon:   string;
  label:  string;
  badge:  string;
}> = {
  Critical: {
    bg:     'bg-red-50',
    border: 'border-red-200',
    icon:   'text-red-600',
    label:  'Critical',
    badge:  'bg-red-100 text-red-700 border-red-300',
  },
  Warning: {
    bg:     'bg-amber-50',
    border: 'border-amber-200',
    icon:   'text-amber-600',
    label:  'Warning',
    badge:  'bg-amber-100 text-amber-700 border-amber-300',
  },
  Info: {
    bg:     'bg-blue-50',
    border: 'border-blue-100',
    icon:   'text-blue-500',
    label:  'Info',
    badge:  'bg-blue-100 text-blue-700 border-blue-200',
  },
};

const SEVERITY_ICONS: Record<AlertSeverity, string> = {
  Critical: '🔴',
  Warning:  '⚠️',
  Info:     'ℹ️',
};

const SEVERITY_ORDER: Record<AlertSeverity, number> = {
  Critical: 0,
  Warning:  1,
  Info:     2,
};

/**
 * AlertsPanel — list of active system alerts sorted by severity then time.
 *
 * Pure server component. Renders each alert with a coloured severity badge
 * and relative timestamp.
 */
export function AlertsPanel({ alerts }: AlertsPanelProps) {
  const sorted = [...alerts].sort((a, b) => {
    const diff = SEVERITY_ORDER[a.severity] - SEVERITY_ORDER[b.severity];
    return diff !== 0 ? diff : b.createdAtUtc.localeCompare(a.createdAtUtc);
  });

  const criticalCount = alerts.filter(a => a.severity === 'Critical').length;
  const warningCount  = alerts.filter(a => a.severity === 'Warning').length;

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

      {/* Header */}
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          System Alerts
        </h2>
        <div className="flex items-center gap-2">
          {criticalCount > 0 && (
            <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-red-100 text-red-700 border border-red-300">
              {criticalCount} Critical
            </span>
          )}
          {warningCount > 0 && (
            <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 border border-amber-300">
              {warningCount} Warning
            </span>
          )}
          <span className="text-xs text-gray-400 tabular-nums">
            {alerts.length} total
          </span>
        </div>
      </div>

      {/* Alert list */}
      {sorted.length === 0 ? (
        <div className="px-5 py-8 text-center text-sm text-gray-400">
          No active alerts.
        </div>
      ) : (
        <div className="divide-y divide-gray-100">
          {sorted.map(alert => (
            <AlertRow key={alert.id} alert={alert} />
          ))}
        </div>
      )}
    </div>
  );
}

// ── AlertRow ──────────────────────────────────────────────────────────────────

function AlertRow({ alert }: { alert: SystemAlert }) {
  const cfg  = SEVERITY_CONFIG[alert.severity];
  const icon = SEVERITY_ICONS[alert.severity];
  const time = formatTimestamp(alert.createdAtUtc);

  return (
    <div className={`flex items-start gap-3.5 px-5 py-3.5 ${cfg.bg} ${cfg.border} border-l-4 border-t-0 border-r-0 border-b-0`}>
      <span className="text-base leading-none mt-0.5 shrink-0" aria-hidden>
        {icon}
      </span>

      <div className="flex-1 min-w-0">
        <p className="text-sm text-gray-800 leading-snug">{alert.message}</p>
        <p className="text-xs text-gray-400 mt-1">{time}</p>
      </div>

      <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border shrink-0 ${cfg.badge}`}>
        {alert.severity}
      </span>
    </div>
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
