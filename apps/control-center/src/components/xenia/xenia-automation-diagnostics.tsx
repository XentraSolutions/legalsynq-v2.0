'use client';

import type { XeniaAutomationDiagnosticsSnapshot } from '@/lib/xenia-api';

interface Props {
  diagnostics: XeniaAutomationDiagnosticsSnapshot;
  dlqCount: number;
}

export function XeniaAutomationDiagnostics({ diagnostics, dlqCount }: Props) {
  const stats = [
    { label: 'Registered', value: diagnostics.registrations.length, color: 'text-blue-700' },
    { label: 'Active Executions', value: diagnostics.activeExecutions, color: 'text-green-700' },
    { label: 'Dead-Lettered', value: dlqCount, color: dlqCount > 0 ? 'text-amber-700' : 'text-gray-500' },
    {
      label: 'Total Executions',
      value: diagnostics.registrations.reduce((s, r) => s + r.totalExecutions, 0),
      color: 'text-gray-700',
    },
  ];

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-gray-800">Automation Diagnostics</h3>
        <span className="text-xs text-gray-400">
          {new Date(diagnostics.generatedAt).toLocaleString()} · v{diagnostics.serviceVersion} · {diagnostics.environment}
        </span>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-4">
        {stats.map((stat) => (
          <div key={stat.label} className="bg-gray-50 rounded-lg p-3">
            <p className={`text-2xl font-bold ${stat.color}`}>{stat.value}</p>
            <p className="text-xs text-gray-500 mt-0.5">{stat.label}</p>
          </div>
        ))}
      </div>

      {diagnostics.dependencies.length > 0 && (
        <div>
          <p className="text-xs font-medium text-gray-600 mb-2">Platform Adapter Dependencies</p>
          <div className="flex flex-wrap gap-2">
            {diagnostics.dependencies.map((dep) => {
              const color =
                dep.availabilityState === 'Available' ? 'bg-green-50 text-green-700 border-green-200'
                : dep.availabilityState === 'Disabled' ? 'bg-gray-50 text-gray-500 border-gray-200'
                : dep.availabilityState === 'Degraded' ? 'bg-amber-50 text-amber-700 border-amber-200'
                : 'bg-red-50 text-red-700 border-red-200';
              return (
                <div
                  key={dep.key}
                  className={`inline-flex items-center gap-1.5 px-2 py-1 rounded border text-xs ${color}`}
                >
                  <span className="font-mono">{dep.key}</span>
                  <span className="opacity-60">·</span>
                  <span>{dep.availabilityState}</span>
                  {!dep.isConfigured && (
                    <span className="opacity-60">(unconfigured)</span>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
