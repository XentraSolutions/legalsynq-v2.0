import type { TenantBillingSummary } from '@/types/control-center';

interface TenantBillingPanelProps {
  summary: TenantBillingSummary | null;
  error?:  string | null;
}

export function TenantBillingPanel({ summary, error }: TenantBillingPanelProps) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 px-6 py-5">
      <div className="flex items-center gap-2 mb-4">
        <i className="ri-money-dollar-circle-line text-[16px] text-gray-500" />
        <h2 className="text-sm font-semibold text-gray-700">Tenant Billing</h2>
        <span className="inline-flex items-center text-[10px] font-semibold px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">
          IN PROGRESS
        </span>
      </div>

      {(error || summary?.error) ? (
        <div className="rounded-lg bg-amber-50 border border-amber-200 px-4 py-3">
          <p className="text-xs text-amber-700 font-medium">
            {error ?? summary?.error}
          </p>
        </div>
      ) : !summary ? (
        <div className="rounded-lg bg-gray-50 border border-gray-200 px-4 py-3">
          <p className="text-xs text-gray-500">Tenant billing data unavailable.</p>
        </div>
      ) : !summary.profileFound ? (
        <div className="rounded-lg bg-gray-50 border border-gray-200 px-4 py-4 text-center">
          <i className="ri-file-list-3-line text-2xl text-gray-300 mb-2 block" />
          <p className="text-sm text-gray-500 font-medium">No billing profile</p>
          <p className="text-xs text-gray-400 mt-1">
            This tenant does not yet have a Tenant Billing profile.
          </p>
        </div>
      ) : (
        <BillingProfileDetail summary={summary} />
      )}
    </div>
  );
}

function BillingProfileDetail({ summary }: { summary: TenantBillingSummary }) {
  const p = summary.profile!;

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-3 text-sm">

        <InfoRow label="Status">
          <StatusPill status={p.status} />
        </InfoRow>

        <InfoRow label="Mode">
          <span className="font-medium text-gray-900">{p.mode || '—'}</span>
        </InfoRow>

        <InfoRow label="Billing Account">
          <span className="font-mono text-xs text-gray-600 truncate">
            {p.billingAccountId}
          </span>
        </InfoRow>

        {p.hostPlatformKey && (
          <InfoRow label="Platform Key">
            <span className="font-mono text-xs text-gray-600">{p.hostPlatformKey}</span>
          </InfoRow>
        )}

        {p.activatedAtUtc && (
          <InfoRow label="Activated">
            <span className="text-gray-700">{formatDate(p.activatedAtUtc)}</span>
          </InfoRow>
        )}

        {p.closedAtUtc && (
          <InfoRow label="Closed">
            <span className="text-red-600">{formatDate(p.closedAtUtc)}</span>
          </InfoRow>
        )}
      </div>

      <p className="text-[11px] text-gray-400 pt-1">
        Checked {formatDateTime(summary.lastCheckedAtUtc)}
      </p>
    </div>
  );
}

function InfoRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs text-gray-500 mb-0.5">{label}</p>
      <div>{children}</div>
    </div>
  );
}

function StatusPill({ status }: { status: string }) {
  const lower = status.toLowerCase();
  const styles =
    lower === 'active'    ? 'bg-green-100 text-green-700 border-green-300' :
    lower === 'inactive'  ? 'bg-gray-100  text-gray-500  border-gray-300'  :
    lower === 'closed'    ? 'bg-red-100   text-red-700   border-red-300'   :
    lower === 'pending'   ? 'bg-blue-100  text-blue-700  border-blue-300'  :
                            'bg-gray-100  text-gray-600  border-gray-300'  ;
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles}`}>
      {status}
    </span>
  );
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC',
    });
  } catch {
    return iso;
  }
}

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
      hour12: false, timeZone: 'UTC', timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}
