import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import {
  getXeniaAutomations,
  getXeniaAutomationDiagnostics,
  getXeniaDeadLetterEntries,
  type XeniaAutomationManifest,
  type XeniaAutomationDiagnosticsSnapshot,
  type XeniaDeadLetterEntry,
} from '@/lib/xenia-api';
import { XeniaAutomationTable } from '@/components/xenia/xenia-automation-table';
import { XeniaAutomationDiagnostics } from '@/components/xenia/xenia-automation-diagnostics';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

export const dynamic = 'force-dynamic';

export default async function XeniaAutomationPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let automations: XeniaAutomationManifest[] = [];
  let diagnostics: XeniaAutomationDiagnosticsSnapshot | null = null;
  let dlq: XeniaDeadLetterEntry[] = [];
  let error = false;

  try {
    [automations, diagnostics, dlq] = await Promise.all([
      getXeniaAutomations(token),
      getXeniaAutomationDiagnostics(token),
      getXeniaDeadLetterEntries(token),
    ]);
  } catch {
    error = true;
  }

  return (
    <div className="space-y-6">
      <div className="mb-2">
        <h2 className="text-xl font-semibold text-gray-900">Automation Registry</h2>
        <p className="text-sm text-gray-500 mt-1">
          Generic automation providers registered with Xenia. Includes email sync, and any future platform modules.
        </p>
      </div>

      {error ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia automation service. Ensure Xenia is running on port 5035.
        </div>
      ) : (
        <>
          {diagnostics && (
            <XeniaAutomationDiagnostics diagnostics={diagnostics} dlqCount={dlq.length} />
          )}

          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
              <p className="text-sm font-medium text-gray-700">
                {automations.length} automation{automations.length !== 1 ? 's' : ''} registered
              </p>
              {dlq.length > 0 && (
                <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-amber-100 text-amber-800">
                  {dlq.length} dead-lettered
                </span>
              )}
            </div>
            <XeniaAutomationTable automations={automations} />
          </div>

          {dlq.length > 0 && (
            <div className="rounded-lg border border-amber-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-amber-100 bg-amber-50">
                <p className="text-sm font-medium text-amber-800">Dead-Letter Queue ({dlq.length})</p>
                <p className="text-xs text-amber-600 mt-0.5">
                  Executions that exceeded max retries. Use the API to retry or abandon.
                </p>
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200 text-sm">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Automation</th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Failure</th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Retries</th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">First Failed</th>
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-100">
                    {dlq.map((entry) => (
                      <tr key={entry.id} className="hover:bg-gray-50">
                        <td className="px-4 py-3">
                          <div className="font-mono text-xs text-gray-700">{entry.automationKey}</div>
                          <div className="text-xs text-gray-400">v{entry.automationVersion}</div>
                        </td>
                        <td className="px-4 py-3">
                          <div className="text-xs font-medium text-red-700">{entry.failureCategory}</div>
                          <div className="text-xs text-gray-500">{entry.safeErrorSummary}</div>
                        </td>
                        <td className="px-4 py-3 text-gray-600 text-xs">{entry.retryCount}</td>
                        <td className="px-4 py-3">
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-amber-100 text-amber-800">
                            {entry.status}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-gray-500 text-xs">
                          {new Date(entry.firstFailedAt).toLocaleString()}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
