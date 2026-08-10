import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getAllSourceHealth, type SourceHealthSnapshot } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function SourceHealthPage() {
  await requirePlatformAdmin();

  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let sources: SourceHealthSnapshot[] = [];
  let serviceError = false;

  try {
    const result = await getAllSourceHealth(token);
    sources = result.items;
  } catch {
    serviceError = true;
  }

  const healthBadge = (h: string) => {
    switch (h) {
      case 'Healthy':     return 'bg-green-100 text-green-800';
      case 'Degraded':    return 'bg-yellow-100 text-yellow-800';
      case 'Unavailable': return 'bg-red-100 text-red-800';
      default:            return 'bg-gray-100 text-gray-500';
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Source Health</h2>
          <p className="text-sm text-gray-500 mt-1">
            Detailed health metrics and lock state for all email sources.
          </p>
        </div>
        <a href="/xenia/email/operations" className="text-xs text-indigo-600 hover:text-indigo-700">
          ← Operations
        </a>
      </div>

      {serviceError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service.
        </div>
      ) : sources.length === 0 ? (
        <div className="rounded-lg border border-gray-200 bg-white p-8 text-center text-sm text-gray-400">
          No email sources configured.
        </div>
      ) : (
        <div className="space-y-4">
          {sources.map(s => (
            <div key={s.sourceId} className="rounded-lg border border-gray-200 bg-white p-5">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900">{s.displayName}</h3>
                  <p className="text-xs text-gray-400 mt-0.5">{s.emailAddress} · {s.providerType}</p>
                </div>
                <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${healthBadge(s.healthStatus)}`}>
                  {s.healthStatus}
                </span>
              </div>

              <dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4 text-sm">
                <div>
                  <dt className="text-xs text-gray-500">Consecutive Failures</dt>
                  <dd className={`font-semibold mt-0.5 ${s.consecutiveFailureCount > 0 ? 'text-red-600' : 'text-gray-900'}`}>
                    {s.consecutiveFailureCount}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-500">Last Successful Sync</dt>
                  <dd className="font-medium text-gray-900 mt-0.5">
                    {s.lastSuccessfulSyncAt
                      ? new Date(s.lastSuccessfulSyncAt).toLocaleString()
                      : '—'}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-500">Next Eligible Sync</dt>
                  <dd className="font-medium text-gray-900 mt-0.5">
                    {s.nextEligibleSyncAt
                      ? new Date(s.nextEligibleSyncAt).toLocaleString()
                      : '—'}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-500">Last Error Code</dt>
                  <dd className={`font-medium mt-0.5 ${s.lastErrorCode ? 'text-red-600' : 'text-gray-400'}`}>
                    {s.lastErrorCode ?? 'None'}
                  </dd>
                </div>
              </dl>

              {/* Lock state */}
              {s.activeLockOwner && (
                <div className="mt-4 rounded-md bg-blue-50 border border-blue-100 p-3">
                  <p className="text-xs font-medium text-blue-700 mb-1">Active Sync Lock</p>
                  <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4 text-xs text-blue-800">
                    <div>
                      <dt className="text-blue-500">Owner</dt>
                      <dd className="font-medium">{s.activeLockOwner}</dd>
                    </div>
                    <div>
                      <dt className="text-blue-500">Expires</dt>
                      <dd className="font-medium">
                        {s.lockExpiresAt ? new Date(s.lockExpiresAt).toLocaleString() : '—'}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-blue-500">Fencing Token</dt>
                      <dd className="font-medium">{s.fencingToken ?? '—'}</dd>
                    </div>
                    <div>
                      <dt className="text-blue-500">Renewal Failures</dt>
                      <dd className={`font-semibold ${(s.renewalFailureCount ?? 0) > 0 ? 'text-red-600' : ''}`}>
                        {s.renewalFailureCount ?? 0}
                      </dd>
                    </div>
                  </dl>
                </div>
              )}

              {s.safeLastErrorSummary && (
                <div className="mt-3 rounded-md bg-red-50 border border-red-100 p-3 text-xs text-red-700">
                  {s.safeLastErrorSummary}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
