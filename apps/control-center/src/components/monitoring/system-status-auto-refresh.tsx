'use client';

import { useState, useEffect, useCallback } from 'react';
import type { MonitoringSummary } from '@/types/control-center';
import { SystemHealthCard }        from './system-health-card';
import { IntegrationStatusTable }  from './integration-status-table';
import { AlertsPanel }             from './alerts-panel';

const REFRESH_INTERVAL_MS = 15_000;

interface Props {
  initialData:  MonitoringSummary | null;
  initialError: string | null;
}

export function SystemStatusAutoRefresh({ initialData, initialError }: Props) {
  const [data,       setData]       = useState<MonitoringSummary | null>(initialData);
  const [error,      setError]      = useState<string | null>(initialError);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);
  const [countdown,  setCountdown]  = useState(REFRESH_INTERVAL_MS / 1000);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => { setLastUpdate(new Date()); }, []);

  const fetchData = useCallback(async () => {
    setRefreshing(true);
    try {
      const res = await fetch('/api/monitoring/summary', { cache: 'no-store' });
      if (!res.ok) throw new Error(`Probe returned ${res.status}`);
      const json: MonitoringSummary = await res.json();
      setData(json);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to refresh monitoring data.');
    } finally {
      setRefreshing(false);
      setLastUpdate(new Date());
      setCountdown(REFRESH_INTERVAL_MS / 1000);
    }
  }, []);

  useEffect(() => {
    const pollId = setInterval(fetchData, REFRESH_INTERVAL_MS);
    return () => clearInterval(pollId);
  }, [fetchData]);

  useEffect(() => {
    const tickId = setInterval(() => {
      setCountdown(prev => (prev <= 1 ? REFRESH_INTERVAL_MS / 1000 : prev - 1));
    }, 1_000);
    return () => clearInterval(tickId);
  }, []);

  const hasAlerts     = (data?.alerts.length ?? 0) > 0;
  const criticalCount = data?.alerts.filter(a => a.severity === 'Critical').length ?? 0;

  const infraServices   = data?.integrations.filter(i => i.category === 'infrastructure') ?? [];
  const productServices = data?.integrations.filter(i => i.category === 'product') ?? [];

  return (
    <>
      {/* Page title row */}
      <div className="mb-6 flex items-start justify-between gap-4">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-md bg-indigo-50 border border-indigo-200 mb-2">
            <span className="text-xs font-semibold text-indigo-700 tracking-wide uppercase">
              LegalSynq
            </span>
          </div>
          <h1 className="text-xl font-semibold text-gray-900">System Health</h1>
          <p className="text-sm text-gray-500 mt-1">
            Real-time view of platform service status and active alerts.
          </p>
        </div>

        <div className="flex flex-col items-end gap-2 shrink-0">
          {criticalCount > 0 && (
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-semibold bg-red-100 text-red-700 border border-red-300">
              {criticalCount} Critical Alert{criticalCount > 1 ? 's' : ''}
            </span>
          )}

          {/* Auto-refresh indicator */}
          <div className="flex items-center gap-2">
            <button
              onClick={fetchData}
              disabled={refreshing}
              className="text-xs text-indigo-600 hover:text-indigo-800 underline underline-offset-2 disabled:opacity-40 disabled:cursor-not-allowed transition-opacity"
            >
              {refreshing ? 'Refreshing…' : 'Refresh now'}
            </button>
            <span className="text-xs text-gray-400">
              · auto in {countdown}s
            </span>
          </div>

          {lastUpdate && (
            <p className="text-[11px] text-gray-400">
              Last updated {lastUpdate.toLocaleTimeString()}
            </p>
          )}
        </div>
      </div>

      {error ? (
        <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
          <p className="text-sm text-red-700 font-medium">Failed to load monitoring data</p>
          <p className="text-xs text-red-600 mt-1">{error}</p>
        </div>
      ) : data ? (
        <div className="space-y-5">
          <SystemHealthCard summary={data.system} />

          <IntegrationStatusTable
            integrations={infraServices}
            title="Platform Services"
            subtitle="Core infrastructure components"
          />

          <IntegrationStatusTable
            integrations={productServices}
            title="Products"
            subtitle="Tenant-facing product services"
          />

          {hasAlerts ? (
            <AlertsPanel alerts={data.alerts} />
          ) : (
            <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
              <p className="text-sm text-gray-500 text-center py-4">No active alerts.</p>
            </div>
          )}
        </div>
      ) : null}
    </>
  );
}
