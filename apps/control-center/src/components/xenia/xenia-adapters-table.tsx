'use client';

import type { XeniaAdapterDto } from '@/lib/xenia-api';
import { XeniaStatusBadge } from './xenia-status-badge';

interface XeniaAdaptersTableProps {
  adapters: XeniaAdapterDto[];
}

export function XeniaAdaptersTable({ adapters }: XeniaAdaptersTableProps) {
  if (adapters.length === 0) {
    return (
      <div className="text-center py-12 text-gray-500">
        <p className="text-sm">No adapters registered.</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Adapter</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Type</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Config</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Availability</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Health</th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Last Check</th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-100">
          {adapters.map((a) => (
            <tr key={a.id} className="hover:bg-gray-50">
              <td className="px-4 py-3">
                <div className="font-medium text-gray-900">{a.name}</div>
                <div className="text-xs text-gray-400 font-mono">{a.adapter_key}</div>
                {a.diagnostic_message && (
                  <div className="text-xs text-amber-600 mt-0.5">{a.diagnostic_message}</div>
                )}
              </td>
              <td className="px-4 py-3 text-gray-600 text-xs">{a.adapter_type}</td>
              <td className="px-4 py-3">
                <XeniaStatusBadge status={a.configuration_status} variant="adapter" />
              </td>
              <td className="px-4 py-3">
                <XeniaStatusBadge status={a.availability_status} variant="adapter" />
              </td>
              <td className="px-4 py-3">
                <XeniaStatusBadge status={a.health_status} variant="adapter" />
              </td>
              <td className="px-4 py-3 text-gray-500 text-xs">
                {a.last_health_check_at
                  ? new Date(a.last_health_check_at).toLocaleString()
                  : <span className="text-gray-300">—</span>
                }
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
