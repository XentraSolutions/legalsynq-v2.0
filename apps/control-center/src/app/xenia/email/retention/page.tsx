import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  getRetentionHistory,
  getEmailOperationalSettings,
  type RetentionRun,
  type EmailOperationalSettings,
} from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function EmailRetentionPage() {
  await requirePlatformAdmin();

  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let history: RetentionRun[]            = [];
  let settings: EmailOperationalSettings | null = null;
  let serviceError = false;

  try {
    [{ items: history }, settings] = await Promise.all([
      getRetentionHistory(token, 20),
      getEmailOperationalSettings(token),
    ]);
  } catch {
    serviceError = true;
  }

  const statusBadge = (status: string) => {
    switch (status) {
      case 'Completed':  return 'bg-green-100 text-green-800';
      case 'Failed':     return 'bg-red-100 text-red-800';
      case 'Running':    return 'bg-blue-100 text-blue-800';
      case 'Cancelled':  return 'bg-yellow-100 text-yellow-800';
      default:           return 'bg-gray-100 text-gray-500';
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Retention Management</h2>
          <p className="text-sm text-gray-500 mt-1">
            Email data retention runs and policy configuration.
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
      ) : (
        <>
          {/* Current policy summary */}
          {settings && (
            <div className="rounded-lg border border-gray-200 bg-white p-5">
              <h3 className="text-sm font-semibold text-gray-900 mb-3">Active Policy</h3>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-4 text-sm">
                <PolicyItem
                  label="Retention Enabled"
                  value={settings.retentionEnabled ? 'Yes' : 'No (dry-run only)'}
                  warn={!settings.retentionEnabled}
                />
                <PolicyItem
                  label="Legal Hold"
                  value={settings.legalHoldEnabled ? 'Active' : 'Inactive'}
                  warn={settings.legalHoldEnabled}
                />
                <PolicyItem
                  label="Message Metadata"
                  value={`${settings.messageMetadataRetentionDays}d`}
                />
                <PolicyItem
                  label="Message Bodies"
                  value={`${settings.messageBodyRetentionDays}d`}
                />
                <PolicyItem
                  label="Ingestion Runs"
                  value={`${settings.ingestionRunRetentionDays}d`}
                />
                <PolicyItem
                  label="Alert History"
                  value={`${settings.alertRetentionDays}d`}
                />
                <PolicyItem
                  label="Dry Run Default"
                  value={settings.retentionDryRunDefault ? 'Yes' : 'No'}
                />
                <PolicyItem
                  label="Purge Batch Size"
                  value={String(settings.purgeBatchSize)}
                />
              </div>
              <div className="mt-3 pt-3 border-t border-gray-100 flex gap-3">
                <a
                  href="/xenia/email/retention/settings"
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 transition-colors"
                >
                  Edit Settings
                </a>
              </div>
            </div>
          )}

          {/* Run action notice */}
          {settings && settings.legalHoldEnabled && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
              Legal hold is active. Deletion runs will be blocked. Only dry-run analysis is available.
            </div>
          )}

          {/* Run history */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100">
              <h3 className="text-sm font-semibold text-gray-900">Retention Run History</h3>
            </div>

            {history.length === 0 ? (
              <div className="px-4 py-10 text-center text-sm text-gray-400">
                No retention runs have been executed.
              </div>
            ) : (
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Mode</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Started</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Messages Eligible</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Deleted</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Bodies Cleared</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Failures</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {history.map(r => (
                    <tr key={r.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${r.mode === 'DryRun' ? 'bg-gray-100 text-gray-600' : 'bg-orange-100 text-orange-800'}`}>
                          {r.mode === 'DryRun' ? 'Dry Run' : 'Execute'}
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusBadge(r.status)}`}>
                          {r.status}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">
                        {new Date(r.startedAt).toLocaleString()}
                      </td>
                      <td className="px-4 py-2.5 text-sm text-gray-700">{r.messagesEligible.toLocaleString()}</td>
                      <td className="px-4 py-2.5 text-sm text-gray-700">{r.messagesDeleted.toLocaleString()}</td>
                      <td className="px-4 py-2.5 text-sm text-gray-700">{r.bodiesCleared.toLocaleString()}</td>
                      <td className="px-4 py-2.5">
                        {r.failures > 0
                          ? <span className="text-sm font-medium text-red-600">{r.failures}</span>
                          : <span className="text-xs text-gray-400">0</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}

function PolicyItem({
  label,
  value,
  warn,
}: {
  label: string;
  value: string;
  warn?: boolean;
}) {
  return (
    <div>
      <p className="text-xs font-medium text-gray-500">{label}</p>
      <p className={`text-sm font-semibold mt-0.5 ${warn ? 'text-amber-600' : 'text-gray-900'}`}>
        {value}
      </p>
    </div>
  );
}
