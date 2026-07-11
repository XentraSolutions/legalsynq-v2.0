import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getAllProviderHealth, type ProviderHealthSnapshot } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function ProviderHealthPage() {
  await requirePlatformAdmin();

  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let providers: ProviderHealthSnapshot[] = [];
  let serviceError = false;

  try {
    const result = await getAllProviderHealth(token);
    providers = result.items;
  } catch {
    serviceError = true;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Provider Health</h2>
          <p className="text-sm text-gray-500 mt-1">
            Aggregate health and activity metrics by email provider type.
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
      ) : providers.length === 0 ? (
        <div className="rounded-lg border border-gray-200 bg-white p-8 text-center text-sm text-gray-400">
          No provider data available.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {providers.map(p => {
            const healthPct = p.totalSources > 0
              ? Math.round((p.healthySources / p.totalSources) * 100)
              : 0;

            const cardBorder =
              p.unavailableSources > 0
                ? 'border-red-200'
                : p.degradedSources > 0
                  ? 'border-yellow-200'
                  : 'border-gray-200';

            return (
              <div key={p.providerType} className={`rounded-lg border ${cardBorder} bg-white p-5`}>
                <div className="flex items-start justify-between mb-4">
                  <div>
                    <h3 className="text-sm font-semibold text-gray-900">{p.displayName}</h3>
                    <p className="text-xs text-gray-400 mt-0.5">{p.providerType}</p>
                  </div>
                  <span className={`text-lg font-bold ${healthPct === 100 ? 'text-green-600' : healthPct >= 50 ? 'text-yellow-600' : 'text-red-600'}`}>
                    {healthPct}%
                  </span>
                </div>

                <dl className="space-y-2">
                  <ProviderMetric label="Total Sources" value={String(p.totalSources)} />
                  <ProviderMetric
                    label="Healthy"
                    value={String(p.healthySources)}
                    accent="green"
                  />
                  {p.degradedSources > 0 && (
                    <ProviderMetric
                      label="Degraded"
                      value={String(p.degradedSources)}
                      accent="yellow"
                    />
                  )}
                  {p.unavailableSources > 0 && (
                    <ProviderMetric
                      label="Unavailable"
                      value={String(p.unavailableSources)}
                      accent="red"
                    />
                  )}
                  {p.recentSuccessRate != null && (
                    <ProviderMetric
                      label="Recent Success Rate"
                      value={`${(p.recentSuccessRate * 100).toFixed(1)}%`}
                    />
                  )}
                  {p.lastActivityAt && (
                    <ProviderMetric
                      label="Last Activity"
                      value={new Date(p.lastActivityAt).toLocaleString()}
                    />
                  )}
                </dl>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function ProviderMetric({
  label,
  value,
  accent,
}: {
  label: string;
  value: string;
  accent?: 'green' | 'yellow' | 'red';
}) {
  const cls =
    accent === 'green'  ? 'text-green-700 font-semibold' :
    accent === 'yellow' ? 'text-yellow-700 font-semibold' :
    accent === 'red'    ? 'text-red-700 font-semibold' :
    'text-gray-900';

  return (
    <div className="flex items-center justify-between">
      <dt className="text-xs text-gray-500">{label}</dt>
      <dd className={`text-sm ${cls}`}>{value}</dd>
    </div>
  );
}
