import type { NavSection } from '@/types';

/**
 * Control Center sidebar navigation — NavSection[] matching the web app structure.
 * All routes are host-root paths (no /control-center prefix — standalone app).
 */
export const CC_NAV: NavSection[] = [
  {
    heading: 'OVERVIEW',
    items: [
      { href: '/tenants',    label: 'Dashboard', icon: 'ri-dashboard-3-line' },
    ],
  },
  {
    heading: 'TENANTS',
    items: [
      { href: '/tenants',      label: 'All Tenants',  icon: 'ri-building-2-line' },
      { href: '/tenant-users', label: 'Tenant Users', icon: 'ri-group-line'      },
    ],
  },
  {
    heading: 'ACCESS CONTROL',
    items: [
      { href: '/roles',    label: 'Roles & Permissions',  icon: 'ri-shield-keyhole-line' },
      { href: '/products', label: 'Product Entitlements', icon: 'ri-apps-line'           },
    ],
  },
  {
    heading: 'OPERATIONS',
    items: [
      { href: '/support',    label: 'Support Tools', icon: 'ri-customer-service-2-line' },
      { href: '/audit-logs', label: 'Audit Logs',    icon: 'ri-file-list-3-line'        },
      { href: '/monitoring', label: 'Monitoring',    icon: 'ri-pulse-line'              },
    ],
  },
  {
    heading: 'CONFIGURATION',
    items: [
      { href: '/settings', label: 'Platform Settings', icon: 'ri-settings-3-line' },
    ],
  },
];

/** @deprecated — kept for any existing callers; remove once all are migrated. */
export function buildCCNav() {
  return CC_NAV;
}
