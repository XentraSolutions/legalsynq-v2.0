'use client';

import type { XeniaAutomationManifest } from '@/lib/xenia-api';
import { XeniaStatusBadge } from './xenia-status-badge';

interface XeniaAutomationTableProps {
  automations: XeniaAutomationManifest[];
}

const LIFECYCLE_COLORS: Record<string, string> = {
  Enabled:     'bg-green-100 text-green-800',
  Disabled:    'bg-gray-100 text-gray-500',
  Registered:  'bg-blue-100 text-blue-700',
  Degraded:    'bg-amber-100 text-amber-800',
  Unavailable: 'bg-red-100 text-red-700',
  Retired:     'bg-slate-100 text-slate-500',
};

export function XeniaAutomationTable({ automations }: XeniaAutomationTableProps) {
  if (automations.length === 0) {
    return (
      <div className="text-center py-12 text-gray-500">
        <p className="text-sm">No automations registered.</p>
        <p className="text-xs mt-1 text-gray-400">
          Automations are registered at service startup via AutomationRegistrationWorker.
        </p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Automation</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Version</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Category</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Dependencies</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Capabilities</th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-100">
          {automations.map((a) => {
            const statusColor = LIFECYCLE_COLORS[a.status] ?? 'bg-gray-100 text-gray-500';
            const mandatoryDeps = a.dependencies.filter((d) => !d.isOptional);
            const unavailableMandatory = mandatoryDeps.filter(
              (d) => d.availabilityState === 'Unavailable',
            );

            return (
              <tr key={a.automationKey} className="hover:bg-gray-50">
                <td className="px-4 py-3">
                  <div className="font-medium text-gray-900">{a.displayName}</div>
                  <div className="text-xs text-gray-400 font-mono">{a.automationKey}</div>
                  {a.description && (
                    <div className="text-xs text-gray-500 mt-0.5 max-w-xs">{a.description}</div>
                  )}
                </td>
                <td className="px-4 py-3 text-gray-600 font-mono text-xs">{a.version}</td>
                <td className="px-4 py-3">
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-purple-100 text-purple-700">
                    {a.category}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${statusColor}`}>
                    {a.status}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="space-y-0.5">
                    {a.dependencies.map((dep) => {
                      const depColor =
                        dep.availabilityState === 'Available' ? 'text-green-600'
                        : dep.availabilityState === 'Disabled' ? 'text-gray-400'
                        : dep.availabilityState === 'Degraded' ? 'text-amber-600'
                        : 'text-red-600';
                      return (
                        <div key={dep.key} className={`text-xs font-mono ${depColor}`}>
                          {dep.key}
                          <span className="text-gray-400 ml-1">
                            ({dep.availabilityState})
                          </span>
                        </div>
                      );
                    })}
                    {a.dependencies.length === 0 && (
                      <span className="text-xs text-gray-400">none</span>
                    )}
                  </div>
                  {unavailableMandatory.length > 0 && (
                    <div className="text-xs text-red-600 mt-1 font-medium">
                      {unavailableMandatory.length} mandatory dep{unavailableMandatory.length !== 1 ? 's' : ''} unavailable
                    </div>
                  )}
                </td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1">
                    {a.tenantEnablementSupported && (
                      <span className="text-xs bg-blue-50 text-blue-700 px-1.5 py-0.5 rounded">tenant</span>
                    )}
                    {a.schedulingSupported && (
                      <span className="text-xs bg-indigo-50 text-indigo-700 px-1.5 py-0.5 rounded">schedule</span>
                    )}
                    {a.diagnosticsSupported && (
                      <span className="text-xs bg-slate-50 text-slate-600 px-1.5 py-0.5 rounded">diag</span>
                    )}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
