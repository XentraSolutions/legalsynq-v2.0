'use client';

interface StatusBadgeProps {
  status: string;
  variant?: 'module' | 'adapter';
}

const MODULE_COLORS: Record<string, string> = {
  Healthy: 'bg-green-100 text-green-800',
  Degraded: 'bg-yellow-100 text-yellow-800',
  Unavailable: 'bg-red-100 text-red-800',
  Unknown: 'bg-gray-100 text-gray-600',
};

const ADAPTER_COLORS: Record<string, string> = {
  Healthy: 'bg-green-100 text-green-800',
  Degraded: 'bg-yellow-100 text-yellow-800',
  Unavailable: 'bg-red-100 text-red-800',
  Unconfigured: 'bg-blue-50 text-blue-700',
  Unknown: 'bg-gray-100 text-gray-600',
};

export function XeniaStatusBadge({ status, variant = 'module' }: StatusBadgeProps) {
  const palette = variant === 'adapter' ? ADAPTER_COLORS : MODULE_COLORS;
  const colorClass = palette[status] ?? 'bg-gray-100 text-gray-600';

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colorClass}`}>
      {status}
    </span>
  );
}
