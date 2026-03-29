import type { NavGroup, PlatformSession } from '@/types';

/**
 * Derives the Control Center sidebar navigation from the session.
 *
 * Only rendered for PlatformAdmin users — the (control-center) layout
 * enforces this via requirePlatformAdmin() before this is ever called.
 *
 * Returns a flat list of NavGroups compatible with the existing Sidebar
 * and ControlCenterShell. All items are always shown to platform admins
 * (no product-role filtering needed here — access is binary: admin or not).
 */
export function buildControlCenterNav(_session: PlatformSession): NavGroup[] {
  return [
    {
      id:    'control-center-overview',
      label: 'Overview',
      icon:  'LayoutDashboard',
      items: [
        { href: '/control-center', label: 'Dashboard' },
      ],
    },
    {
      id:    'control-center-tenants',
      label: 'Tenants',
      icon:  'Building2',
      items: [
        { href: '/control-center/tenants',      label: 'All Tenants' },
        { href: '/control-center/tenant-users', label: 'Tenant Users' },
      ],
    },
    {
      id:    'control-center-access',
      label: 'Access Control',
      icon:  'ShieldCheck',
      items: [
        { href: '/control-center/roles',    label: 'Roles & Permissions' },
        { href: '/control-center/products', label: 'Product Entitlements' },
      ],
    },
    {
      id:    'control-center-ops',
      label: 'Operations',
      icon:  'Wrench',
      items: [
        { href: '/control-center/support',    label: 'Support Tools' },
        { href: '/control-center/audit-logs', label: 'Audit Logs' },
        { href: '/control-center/monitoring', label: 'Monitoring' },
      ],
    },
    {
      id:    'control-center-config',
      label: 'Configuration',
      icon:  'Settings',
      items: [
        { href: '/control-center/settings', label: 'Platform Settings' },
      ],
    },
  ];
}
