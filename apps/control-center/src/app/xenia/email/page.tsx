import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  getEmailModuleState,
  getEmailSources,
  type EmailModuleState,
  type EmailSource,
} from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function XeniaEmailPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let moduleState: EmailModuleState | null = null;
  let sources: EmailSource[] = [];
  let serviceError = false;

  try {
    moduleState = await getEmailModuleState(token);
    const result = await getEmailSources(token);
    sources = result.sources;
  } catch {
    serviceError = true;
  }

  const statusColor = (status: string) => {
    switch (status) {
      case 'Active': return 'bg-green-100 text-green-800';
      case 'Disabled': return 'bg-gray-100 text-gray-600';
      case 'Error': return 'bg-red-100 text-red-800';
      case 'Pending': return 'bg-yellow-100 text-yellow-800';
      case 'Validating': return 'bg-blue-100 text-blue-800';
      default: return 'bg-gray-100 text-gray-600';
    }
  };

  const healthColor = (health: string) => {
    switch (health) {
      case 'Healthy': return 'text-green-600';
      case 'Degraded': return 'text-yellow-600';
      case 'Unavailable': return 'text-red-600';
      default: return 'text-gray-400';
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-gray-900">Email Module</h2>
        <p className="text-sm text-gray-500 mt-1">
          Tenant-scoped email source connectivity and automation foundation.
        </p>
      </div>

      {serviceError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service. Ensure it is running on port 5035.
        </div>
      ) : (
        <>
          {/* Module status card */}
          {moduleState && (
            <div className="rounded-lg border border-gray-200 bg-white p-5">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900">Module Status</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    {moduleState.name} — v{moduleState.version}
                  </p>
                </div>
                <div className="flex gap-2">
                  <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${moduleState.globalEnabled ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-600'}`}>
                    Global: {moduleState.globalEnabled ? 'Enabled' : 'Disabled'}
                  </span>
                  <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${moduleState.effectiveEnabled ? 'bg-indigo-100 text-indigo-800' : 'bg-gray-100 text-gray-600'}`}>
                    {moduleState.effectiveEnabled ? 'Active' : 'Inactive'}
                  </span>
                </div>
              </div>
            </div>
          )}

          {/* Sources summary */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <StatCard
              label="Total Sources"
              value={sources.length}
              sublabel="across all providers"
            />
            <StatCard
              label="Active"
              value={sources.filter(s => s.status === 'Active').length}
              sublabel="ready"
            />
            <StatCard
              label="Healthy"
              value={sources.filter(s => s.healthStatus === 'Healthy').length}
              sublabel="last validation passed"
            />
          </div>

          {/* Sources table */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-gray-900">Email Sources</h3>
              <a
                href="/xenia/email/sources"
                className="text-xs font-medium text-indigo-600 hover:text-indigo-700"
              >
                Manage →
              </a>
            </div>

            {sources.length === 0 ? (
              <div className="px-4 py-8 text-center text-sm text-gray-500">
                No email sources configured.{' '}
                <a href="/xenia/email/sources" className="text-indigo-600 hover:underline">
                  Add one
                </a>
              </div>
            ) : (
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Provider</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Health</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Validation</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {sources.map(s => (
                    <tr key={s.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-2.5">
                        <div className="text-sm font-medium text-gray-900">{s.displayName}</div>
                        <div className="text-xs text-gray-500">{s.emailAddress}</div>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="text-sm text-gray-700">{s.providerType}</span>
                        <div className="text-xs text-gray-400">{s.authType}</div>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${statusColor(s.status)}`}>
                          {s.status}
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className={`text-sm font-medium ${healthColor(s.healthStatus)}`}>
                          {s.healthStatus}
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="text-xs text-gray-500">{s.validationStatus}</span>
                        {s.lastValidationLatencyMs != null && (
                          <span className="text-xs text-gray-400 ml-1">({s.lastValidationLatencyMs}ms)</span>
                        )}
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

function StatCard({ label, value, sublabel }: { label: string; value: number; sublabel: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wider">{label}</p>
      <p className="text-2xl font-bold text-gray-900 mt-1">{value}</p>
      <p className="text-xs text-gray-400 mt-0.5">{sublabel}</p>
    </div>
  );
}
