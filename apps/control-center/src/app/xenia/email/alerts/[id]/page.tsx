import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  getEmailAlert,
  acknowledgeAlert,
  resolveAlert,
  suppressAlert,
  type OperationalAlert,
} from '@/lib/xenia-email-api';
import { revalidatePath } from 'next/cache';
import Link from 'next/link';

export const dynamic = 'force-dynamic';

export default async function AlertDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requirePlatformAdmin();

  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';
  const { id } = await params;

  let alert: OperationalAlert | null = null;
  let serviceError = false;

  try {
    alert = await getEmailAlert(token, id);
  } catch {
    serviceError = true;
  }

  const severityBadge = (severity: string) => {
    switch (severity) {
      case 'Critical':      return 'bg-red-100 text-red-800 border border-red-200';
      case 'Warning':       return 'bg-yellow-100 text-yellow-800 border border-yellow-200';
      case 'Informational': return 'bg-blue-100 text-blue-800 border border-blue-200';
      default:              return 'bg-gray-100 text-gray-700';
    }
  };

  const statusBadge = (status: string) => {
    switch (status) {
      case 'Open':         return 'bg-orange-100 text-orange-800';
      case 'Acknowledged': return 'bg-purple-100 text-purple-800';
      case 'Resolved':     return 'bg-green-100 text-green-800';
      case 'Suppressed':   return 'bg-gray-100 text-gray-600';
      default:             return 'bg-gray-100 text-gray-700';
    }
  };

  const fmt = (v: string | undefined) =>
    v ? new Date(v).toLocaleString() : '—';

  async function doAcknowledge() {
    'use server';
    const jar2  = await cookies();
    const tok   = jar2.get(SESSION_COOKIE_NAME)?.value ?? '';
    try { await acknowledgeAlert(tok, id); } catch { /* handled gracefully */ }
    revalidatePath(`/xenia/email/alerts/${id}`);
  }

  async function doResolve() {
    'use server';
    const jar2 = await cookies();
    const tok  = jar2.get(SESSION_COOKIE_NAME)?.value ?? '';
    try { await resolveAlert(tok, id); } catch { /* handled gracefully */ }
    revalidatePath(`/xenia/email/alerts/${id}`);
  }

  async function doSuppress(formData: FormData) {
    'use server';
    const jar2    = await cookies();
    const tok     = jar2.get(SESSION_COOKIE_NAME)?.value ?? '';
    const minutes = Number(formData.get('suppressMinutes') ?? '60');
    try { await suppressAlert(tok, id, minutes); } catch { /* handled gracefully */ }
    revalidatePath(`/xenia/email/alerts/${id}`);
  }

  if (serviceError || !alert) {
    return (
      <div className="p-8 space-y-4">
        <Link href="/xenia/email/alerts" className="text-sm text-indigo-600 hover:underline">
          ← Back to Alerts
        </Link>
        {serviceError ? (
          <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
            Unable to load alert. The service may be unavailable.
          </div>
        ) : (
          <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 text-yellow-700">
            Alert not found.
          </div>
        )}
      </div>
    );
  }

  const canAcknowledge = alert.status === 'Open';
  const canResolve     = alert.status === 'Open' || alert.status === 'Acknowledged';
  const canSuppress    = alert.status === 'Open' || alert.status === 'Acknowledged';

  return (
    <div className="p-8 max-w-3xl space-y-6">
      <div>
        <Link href="/xenia/email/alerts" className="text-sm text-indigo-600 hover:underline">
          ← Alerts
        </Link>
        <div className="flex items-start justify-between gap-4 mt-2">
          <h1 className="text-2xl font-bold text-gray-900">{alert.title}</h1>
          <div className="flex gap-2 flex-shrink-0">
            <span className={`px-2 py-1 rounded text-xs font-semibold ${severityBadge(alert.severity)}`}>
              {alert.severity}
            </span>
            <span className={`px-2 py-1 rounded text-xs font-semibold ${statusBadge(alert.status)}`}>
              {alert.status}
            </span>
          </div>
        </div>
      </div>

      <div className="bg-white border border-gray-200 rounded-lg divide-y divide-gray-100 text-sm">
        <Row label="Alert Type"    value={alert.alertType} />
        <Row label="Source ID"     value={alert.emailSourceId ?? '—'} />
        <Row label="Occurrences"   value={String(alert.occurrenceCount)} />
        <Row label="First Seen"    value={fmt(alert.firstObservedAt)} />
        <Row label="Last Seen"     value={fmt(alert.lastObservedAt)} />
        {alert.acknowledgedAt  && <Row label="Acknowledged"    value={fmt(alert.acknowledgedAt)} />}
        {alert.resolvedAt      && <Row label="Resolved"        value={fmt(alert.resolvedAt)} />}
        {alert.suppressedUntil && <Row label="Suppressed Until" value={fmt(alert.suppressedUntil)} />}
        {alert.correlationId   && <Row label="Correlation ID"   value={alert.correlationId} mono />}
      </div>

      {alert.safeDescription && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
          <p className="text-xs font-medium text-gray-500 mb-1 uppercase tracking-wide">Details</p>
          <p className="text-sm text-gray-700">{alert.safeDescription}</p>
        </div>
      )}

      <div className="flex flex-wrap gap-3 items-center">
        {canAcknowledge && (
          <form action={doAcknowledge}>
            <button
              type="submit"
              className="px-4 py-2 text-sm font-medium bg-purple-600 text-white rounded-md hover:bg-purple-700"
            >
              Acknowledge
            </button>
          </form>
        )}
        {canResolve && (
          <form action={doResolve}>
            <button
              type="submit"
              className="px-4 py-2 text-sm font-medium bg-green-600 text-white rounded-md hover:bg-green-700"
            >
              Resolve
            </button>
          </form>
        )}
        {canSuppress && (
          <form action={doSuppress} className="flex items-center gap-2">
            <select
              name="suppressMinutes"
              className="text-sm border border-gray-300 rounded-md px-2 py-1.5"
              defaultValue="60"
            >
              <option value="30">30 min</option>
              <option value="60">1 hour</option>
              <option value="240">4 hours</option>
              <option value="1440">24 hours</option>
            </select>
            <button
              type="submit"
              className="px-4 py-2 text-sm font-medium bg-gray-600 text-white rounded-md hover:bg-gray-700"
            >
              Suppress
            </button>
          </form>
        )}
      </div>
    </div>
  );
}

function Row({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex px-4 py-3 gap-4">
      <span className="w-40 flex-shrink-0 text-sm font-medium text-gray-500">{label}</span>
      <span className={`text-sm text-gray-900 ${mono ? 'font-mono text-xs' : ''}`}>{value}</span>
    </div>
  );
}
