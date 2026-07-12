'use client';

import type { XeniaModuleDto } from '@/lib/xenia-api';
import { XeniaStatusBadge } from './xenia-status-badge';

interface XeniaModulesTableProps {
  modules: XeniaModuleDto[];
}

export function XeniaModulesTable({ modules }: XeniaModulesTableProps) {
  if (modules.length === 0) {
    return (
      <div className="text-center py-12 text-gray-500">
        <p className="text-sm">No modules registered.</p>
        <p className="text-xs mt-1 text-gray-400">
          Modules are registered at service startup. Email automation will appear here once XENIA-P1-T2 is complete.
        </p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Module</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Version</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Enabled</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Config Namespace</th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-100">
          {modules.map((m) => (
            <tr key={m.id} className="hover:bg-gray-50">
              <td className="px-4 py-3">
                <div className="font-medium text-gray-900">{m.name}</div>
                <div className="text-xs text-gray-400 font-mono">{m.module_key}</div>
                {m.description && (
                  <div className="text-xs text-gray-500 mt-0.5">{m.description}</div>
                )}
              </td>
              <td className="px-4 py-3 text-gray-600 font-mono text-xs">{m.version}</td>
              <td className="px-4 py-3">
                <XeniaStatusBadge status={m.status} variant="module" />
              </td>
              <td className="px-4 py-3">
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                  m.global_enabled ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-500'
                }`}>
                  {m.global_enabled ? 'Enabled' : 'Disabled'}
                </span>
              </td>
              <td className="px-4 py-3 text-gray-500 font-mono text-xs">{m.configuration_namespace}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
