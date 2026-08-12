'use client';

import type { XeniaServiceInfo, XeniaReadyResponse, XeniaModuleDto, XeniaAdapterDto } from '@/lib/xenia-api';

interface XeniaDashboardProps {
  info: XeniaServiceInfo | null;
  ready: XeniaReadyResponse | null;
  modules: XeniaModuleDto[];
  adapters: XeniaAdapterDto[];
  isServiceReachable: boolean;
}

export function XeniaDashboard({
  info,
  ready,
  modules,
  adapters,
  isServiceReachable,
}: XeniaDashboardProps) {
  const enabledModules = modules.filter((m) => m.global_enabled).length;
  const configuredAdapters = adapters.filter((a) => a.configuration_status === 'Healthy').length;
  const healthyAdapters = adapters.filter((a) => a.health_status === 'Healthy').length;

  return (
    <div className="space-y-6">
      {/* Service status card */}
      <div className={`rounded-lg border p-5 ${isServiceReachable ? 'border-green-200 bg-green-50' : 'border-red-200 bg-red-50'}`}>
        <div className="flex items-center gap-3">
          <span className={`h-3 w-3 rounded-full ${isServiceReachable ? 'bg-green-500' : 'bg-red-400'}`} />
          <div>
            <p className="font-semibold text-gray-900">
              {isServiceReachable ? 'Xenia Service Online' : 'Xenia Service Unreachable'}
            </p>
            {info && (
              <p className="text-xs text-gray-500 mt-0.5">
                v{info.version} · {info.environment} ·
                Uptime: {Math.floor(info.uptime_seconds / 60)}m {Math.floor(info.uptime_seconds % 60)}s
              </p>
            )}
            {!isServiceReachable && (
              <p className="text-xs text-red-600 mt-0.5">
                Ensure Xenia is running on port 5035 and XENIA_API_BASE is configured.
              </p>
            )}
          </div>
        </div>
      </div>

      {/* Readiness check */}
      {ready && (
        <div className={`rounded-lg border p-4 ${ready.status === 'ready' ? 'border-green-200 bg-green-50' : 'border-amber-200 bg-amber-50'}`}>
          <p className="text-sm font-medium text-gray-700">
            Readiness: <span className={ready.status === 'ready' ? 'text-green-700' : 'text-amber-700'}>{ready.status}</span>
          </p>
        </div>
      )}

      {/* Summary metrics */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <MetricCard label="Registered Modules" value={modules.length} sub={`${enabledModules} enabled`} />
        <MetricCard label="Platform Adapters" value={adapters.length} sub={`${configuredAdapters} configured`} />
        <MetricCard label="Healthy Adapters" value={healthyAdapters} sub={`of ${adapters.length} total`} />
      </div>

      {/* Service metadata */}
      {info && (
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <h3 className="text-sm font-semibold text-gray-700 mb-3">Service Information</h3>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
            <InfoRow label="Service" value={info.service} />
            <InfoRow label="Version" value={info.version} />
            <InfoRow label="Environment" value={info.environment} />
            <InfoRow label="Standalone" value={info.is_standalone ? 'Yes' : 'No'} />
            <InfoRow label="Started At" value={new Date(info.started_at).toLocaleString()} />
          </dl>
        </div>
      )}

      {/* Adapter status note */}
      {adapters.some((a) => a.configuration_status === 'Unconfigured') && (
        <div className="rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-700">
          <strong>Note:</strong> {adapters.filter((a) => a.configuration_status === 'Unconfigured').length} adapter(s) are unconfigured.
          This is expected for the core foundation — real adapters will be wired in subsequent tickets.
          Unconfigured adapters are honest about their status and never report false success.
        </div>
      )}
    </div>
  );
}

function MetricCard({ label, value, sub }: { label: string; value: number; sub: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <p className="text-xs text-gray-500 uppercase tracking-wide">{label}</p>
      <p className="text-3xl font-bold text-gray-900 mt-1">{value}</p>
      <p className="text-xs text-gray-400 mt-1">{sub}</p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-gray-500">{label}</dt>
      <dd className="text-gray-900 font-mono text-xs">{value}</dd>
    </>
  );
}
