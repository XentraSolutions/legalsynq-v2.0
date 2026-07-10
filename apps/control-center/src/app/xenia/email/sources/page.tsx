import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailSources, type EmailSource } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function EmailSourcesPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let sources: EmailSource[] = [];
  let error = false;

  try {
    const result = await getEmailSources(token);
    sources = result.sources;
  } catch {
    error = true;
  }

  const statusBadge = (status: string) => {
    const map: Record<string, string> = {
      Active: 'bg-green-100 text-green-800',
      Disabled: 'bg-gray-100 text-gray-600',
      Error: 'bg-red-100 text-red-800',
      Pending: 'bg-yellow-100 text-yellow-800',
      Validating: 'bg-blue-100 text-blue-800',
    };
    return map[status] ?? 'bg-gray-100 text-gray-600';
  };

  const healthBadge = (h: string) => {
    const map: Record<string, string> = {
      Healthy: 'bg-green-100 text-green-800',
      Degraded: 'bg-yellow-100 text-yellow-800',
      Unavailable: 'bg-red-100 text-red-800',
      Unknown: 'bg-gray-100 text-gray-500',
    };
    return map[h] ?? 'bg-gray-100 text-gray-500';
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Email Sources</h2>
          <p className="text-sm text-gray-500 mt-1">
            Tenant-scoped mailbox connections. Credentials are stored by reference only — never in plain text.
          </p>
        </div>
        <div className="flex gap-2">
          <a
            href="/xenia/email"
            className="inline-flex items-center gap-1 rounded-md border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
          >
            ← Email Dashboard
          </a>
        </div>
      </div>

      {error ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service. Ensure it is running on port 5035.
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
            <p className="text-sm font-medium text-gray-700">
              {sources.length} source{sources.length !== 1 ? 's' : ''}
            </p>
          </div>

          {sources.length === 0 ? (
            <div className="px-4 py-12 text-center">
              <div className="mx-auto h-12 w-12 rounded-full bg-indigo-50 flex items-center justify-center mb-3">
                <span className="text-indigo-500 text-lg">✉</span>
              </div>
              <p className="text-sm font-medium text-gray-900">No email sources</p>
              <p className="text-xs text-gray-500 mt-1">
                Email sources are created via the Xenia API or tenant portal.
              </p>
            </div>
          ) : (
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead>
                <tr className="bg-gray-50">
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Source</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Provider / Auth</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Connection</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Health</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Last Validated</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Secret</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {sources.map(s => (
                  <tr key={s.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3">
                      <div className="font-medium text-gray-900">{s.displayName}</div>
                      <div className="text-xs text-gray-500">{s.emailAddress}</div>
                      {s.description && (
                        <div className="text-xs text-gray-400 mt-0.5 max-w-xs truncate">{s.description}</div>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-medium text-gray-800">{s.providerType}</div>
                      <div className="text-xs text-gray-500">{s.authType}</div>
                    </td>
                    <td className="px-4 py-3">
                      {s.incomingHost ? (
                        <div>
                          <div className="text-xs font-mono text-gray-700">{s.incomingHost}:{s.incomingPort ?? '—'}</div>
                          <div className="text-xs text-gray-400">{s.useTls ? 'TLS' : 'Plaintext'}</div>
                        </div>
                      ) : (
                        <span className="text-xs text-gray-400">API-based</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusBadge(s.status)}`}>
                        {s.status}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${healthBadge(s.healthStatus)}`}>
                        {s.healthStatus}
                      </span>
                      {s.lastValidationLatencyMs != null && (
                        <div className="text-xs text-gray-400 mt-0.5">{s.lastValidationLatencyMs}ms</div>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {s.lastValidatedAt ? (
                        <div>
                          <div className="text-xs text-gray-700">
                            {new Date(s.lastValidatedAt).toLocaleDateString()}
                          </div>
                          <div className="text-xs text-gray-400">{s.validationStatus}</div>
                        </div>
                      ) : (
                        <span className="text-xs text-gray-400">Never</span>
                      )}
                      {s.lastErrorCode && (
                        <div className="text-xs text-red-600 mt-0.5">{s.lastErrorCode}</div>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {s.hasSecretReference ? (
                        <span className="inline-flex items-center gap-1 text-xs text-green-600">
                          <span className="h-1.5 w-1.5 rounded-full bg-green-500 inline-block" />
                          Ref set
                        </span>
                      ) : (
                        <span className="text-xs text-gray-400">No ref</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Security notice */}
      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
        <p className="text-xs text-amber-800 font-medium">Security note</p>
        <p className="text-xs text-amber-700 mt-0.5">
          Credentials are never stored in plain text. The <strong>Secret</strong> column shows only whether
          a secret reference is configured — the reference ID is not visible here.
          Actual credentials are resolved at runtime via the platform secret service.
        </p>
      </div>
    </div>
  );
}
