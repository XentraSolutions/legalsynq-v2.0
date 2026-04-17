import type { OutboxStatus } from '@/lib/system-health-audit-outbox';

interface AuditOutboxBannerProps {
  status: OutboxStatus;
}

function fmtTime(iso: string | null): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

export function AuditOutboxBanner({ status }: AuditOutboxBannerProps) {
  const hasPersistent = status.persistentFailures > 0;
  const tone = hasPersistent
    ? 'bg-red-50 border-red-200 text-red-900'
    : 'bg-amber-50 border-amber-200 text-amber-900';
  const headline = hasPersistent
    ? 'Some monitoring-config audit events could not be delivered to the central Audit Logs'
    : 'Monitoring-config audit events are queued for delivery to the central Audit Logs';

  return (
    <div className={`border rounded-md px-4 py-3 text-sm ${tone}`} role="alert">
      <p className="font-semibold">{headline}</p>
      <p className="mt-1 text-xs">
        {hasPersistent ? (
          <>
            {status.persistentFailures} event{status.persistentFailures === 1 ? '' : 's'} have
            exhausted automatic retries
            {status.pending - status.persistentFailures > 0 && (
              <> and {status.pending - status.persistentFailures} more {status.pending - status.persistentFailures === 1 ? 'is' : 'are'} still being retried</>
            )}
            . The local audit copy below is intact, but the central Audit Logs page may be
            missing these entries until the audit service recovers and an operator triggers
            redelivery.
          </>
        ) : (
          <>
            {status.pending} event{status.pending === 1 ? '' : 's'} pending. Retries continue
            automatically; entries will appear in the central Audit Logs once the audit
            service is reachable, with their original timestamps preserved.
          </>
        )}
      </p>
      {status.oldestEnqueuedAt && (
        <p className="mt-1 text-xs opacity-75">
          Oldest queued event: <span className="font-mono">{fmtTime(status.oldestEnqueuedAt)}</span>
        </p>
      )}
      {status.lastError && (
        <p className="mt-1 text-xs opacity-75">
          Last error: <span className="font-mono">{status.lastError}</span>
        </p>
      )}
    </div>
  );
}
