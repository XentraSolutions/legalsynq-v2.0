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
      items: [
        { href: '/tenants', label: 'Dashboard', riIcon: 'ri-dashboard-3-line' },
      ],
    },
    {
      id:    'tenants',
      label: 'Tenants',
      items: [
        { href: '/tenants',      label: 'All Tenants',  riIcon: 'ri-building-2-line' },
        { href: '/tenant-users', label: 'Tenant Users', riIcon: 'ri-group-line' },
      ],
    },
    {
      id:    'access',
      label: 'Access Control',
      items: [
        { href: '/roles',    label: 'Roles & Permissions',  riIcon: 'ri-shield-keyhole-line' },
        { href: '/products', label: 'Product Entitlements', riIcon: 'ri-apps-line' },
      ],
    },
    {
      id:    'ops',
      label: 'Operations',
      items: [
        { href: '/support',    label: 'Support Tools', riIcon: 'ri-customer-service-2-line' },
        { href: '/audit-logs', label: 'Audit Logs',    riIcon: 'ri-file-list-3-line' },
        { href: '/monitoring', label: 'Monitoring',    riIcon: 'ri-pulse-line' },
      ],
    },
    {
      id:    'config',
      label: 'Configuration',
      items: [
        { href: '/settings', label: 'Platform Settings', riIcon: 'ri-settings-3-line' },
      ],
    },
  ];
}
