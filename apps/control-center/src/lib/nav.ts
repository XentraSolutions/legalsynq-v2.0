import type { NavGroup } from '@/types';

/**
 * Control Center sidebar navigation.
 * All routes are host-root paths (no /control-center prefix — this is a standalone app).
 */
export function buildCCNav(): NavGroup[] {
  return [
    {
      id:    'overview',
      label: 'Overview',
      items: [{ href: '/tenants', label: 'Dashboard' }],
    },
    {
      id:    'tenants',
      label: 'Tenants',
      items: [
        { href: '/tenants',      label: 'All Tenants' },
        { href: '/tenant-users', label: 'Tenant Users' },
      ],
    },
    {
      id:    'access',
      label: 'Access Control',
      items: [
        { href: '/roles',    label: 'Roles & Permissions' },
        { href: '/products', label: 'Product Entitlements' },
      ],
    },
    {
      id:    'ops',
      label: 'Operations',
      items: [
        { href: '/support',    label: 'Support Tools' },
        { href: '/audit-logs', label: 'Audit Logs' },
        { href: '/monitoring', label: 'Monitoring' },
      ],
    },
    {
      id:    'config',
      label: 'Configuration',
      items: [{ href: '/settings', label: 'Platform Settings' }],
    },
  ];
}
