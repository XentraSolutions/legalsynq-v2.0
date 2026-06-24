import type { CommerceServiceStatus, CommerceReadinessCheck } from '@/types/control-center';

interface CommerceServiceCardProps {
  status:     CommerceServiceStatus;
  latencyMs?: number;
  checkedAt:  string;
}

const STATUS_CONFIG: Record<CommerceServiceStatus, {
  bg:    string;
  ring:  string;
  dot:   string;
  text:  string;
  label: string;
}> = {
  online:   { bg: 'bg-green-50', ring: 'ring-green-200', dot: 'bg-green-500', text: 'text-green-700', label: 'Commerce Service Online'  },
  degraded: { bg: 'bg-amber-50', ring: 'ring-amber-200', dot: 'bg-amber-500', text: 'text-amber-700', label: 'Commerce Service Degraded' },
  offline:  { bg: 'bg-red-50',   ring: 'ring-red-200',   dot: 'bg-red-600',   text: 'text-red-700',   label: 'Commerce Service Offline'  },
};

export function CommerceServiceCard({ status, latencyMs, checkedAt }: CommerceServiceCardProps) {
  const cfg   = STATUS_CONFIG[status];
  const since = formatTime(checkedAt);

  return (
    <div className={`rounded-xl ring-1 ${cfg.bg} ${cfg.ring} px-6 py-5 flex items-center gap-4`}>
      <span className="relative flex h-4 w-4 shrink-0">
        {status === 'online' && (
          <span className={`animate-ping absolute inline-flex h-full w-full rounded-full ${cfg.dot} opacity-40`} />
        )}
        <span className={`relative inline-flex rounded-full h-4 w-4 ${cfg.dot}`} />
      </span>

      <div className="flex-1 min-w-0">
        <p className={`text-base font-semibold ${cfg.text}`}>{cfg.label}</p>
        <p className="text-xs text-gray-500 mt-0.5">
          {latencyMs !== undefined && (
            <>Latency: <span className="font-medium">{latencyMs} ms</span>{' · '}</>
          )}
          Last checked {since}
        </p>
      </div>

      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border shrink-0 ${
        status === 'online'   ? 'bg-green-100 text-green-700 border-green-300' :
        status === 'degraded' ? 'bg-amber-100 text-amber-700 border-amber-300' :
                                'bg-red-100   text-red-700   border-red-300'
      }`}>
        {status === 'online' ? 'Online' : status === 'degraded' ? 'Degraded' : 'Offline'}
      </span>
    </div>
  );
}

interface CommerceReadinessPanelProps {
  checks: CommerceReadinessCheck[];
}

const CHECK_CONFIG: Record<CommerceReadinessCheck['status'], { dot: string; label: string }> = {
  ok:      { dot: 'bg-green-500', label: 'OK'      },
  degraded: { dot: 'bg-amber-500', label: 'Degraded' },
  error:   { dot: 'bg-red-500',   label: 'Error'   },
};

export function CommerceReadinessPanel({ checks }: CommerceReadinessPanelProps) {
  if (checks.length === 0) return null;

  return (
    <div className="bg-white rounded-xl border border-gray-200 px-6 py-5">
      <h2 className="text-sm font-semibold text-gray-700 mb-3">Readiness Checks</h2>
      <ul className="space-y-2">
        {checks.map(c => {
          const cfg = CHECK_CONFIG[c.status];
          return (
            <li key={c.name} className="flex items-center gap-3">
              <span className={`h-2.5 w-2.5 rounded-full shrink-0 ${cfg.dot}`} />
              <span className="text-sm text-gray-700 flex-1 font-mono">{c.name}</span>
              <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
                c.status === 'ok'      ? 'bg-green-100 text-green-700' :
                c.status === 'degraded' ? 'bg-amber-100 text-amber-700' :
                                         'bg-red-100   text-red-700'
              }`}>
                {cfg.label}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour: '2-digit', minute: '2-digit', second: '2-digit',
      hour12: false, timeZone: 'UTC', timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}
