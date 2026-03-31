import type { NavSection } from '@/types';

/**
 * Control Center sidebar navigation — NavSection[] with status badges.
 *
 * Badge values:
 *   LIVE        — fully wired to a working backend endpoint
 *   IN PROGRESS — partially wired or mixed live/mock data
 *   MOCKUP      — no real backend wiring; placeholder UI only
 *
 * Sections are ordered: LIVE functionality first, MOCKUP last.
 */
export const CC_NAV: NavSection[] = [
  {
    heading: 'OVERVIEW',
    items: [
      { href: '/', label: 'Dashboard', icon: 'ri-dashboard-3-line' },
    ],
  },

  {
    heading: 'PLATFORM',
    items: [
      { href: '/platform-readiness', label: 'Platform Readiness', icon: 'ri-checkbox-circle-line' },
      { href: '/legacy-coverage',    label: 'Legacy Coverage',    icon: 'ri-history-fill'         },
    ],
  },

  {
    heading: 'IDENTITY',
    items: [
      { href: '/tenant-users', label: 'Users',            icon: 'ri-group-line'           },
      { href: '/roles',        label: 'Roles',            icon: 'ri-shield-keyhole-line'  },
      { href: '/scoped-roles', label: 'Scoped Roles',     icon: 'ri-focus-3-line', badge: 'MOCKUP' },
      { href: '/org-types',    label: 'Org Types',        icon: 'ri-building-4-line'      },
    ],
  },

  {
    heading: 'RELATIONSHIPS',
    items: [
      { href: '/relationship-types', label: 'Relationship Types', icon: 'ri-links-line'         },
      { href: '/org-relationships',  label: 'Org Relationships',  icon: 'ri-share-circle-line'  },
    ],
  },

  {
    heading: 'PRODUCT RULES',
    items: [
      { href: '/product-rules', label: 'Access Rules', icon: 'ri-shield-check-line' },
    ],
  },

  {
    heading: 'CARECONNECT',
    items: [
      { href: '/careconnect-integrity', label: 'Integrity', icon: 'ri-heart-pulse-line' },
    ],
  },

  {
    heading: 'TENANTS',
    items: [
      { href: '/tenants', label: 'Tenants',       icon: 'ri-building-2-line'                    },
      { href: '/domains', label: 'Tenant Domains', icon: 'ri-global-line', badge: 'MOCKUP' },
    ],
  },

  {
    heading: 'SYNQAUDIT',
    items: [
      { href: '/synqaudit',             label: 'Overview',       icon: 'ri-shield-check-line',  badge: 'LIVE' },
      { href: '/synqaudit/investigation', label: 'Investigation', icon: 'ri-search-eye-line',    badge: 'LIVE' },
      { href: '/synqaudit/trace',       label: 'Trace Viewer',   icon: 'ri-git-branch-line',    badge: 'LIVE' },
      { href: '/synqaudit/exports',     label: 'Exports',        icon: 'ri-download-cloud-line', badge: 'LIVE' },
      { href: '/synqaudit/integrity',   label: 'Integrity',      icon: 'ri-fingerprint-line',   badge: 'LIVE' },
      { href: '/synqaudit/legal-holds', label: 'Legal Holds',    icon: 'ri-scales-3-line',      badge: 'LIVE' },
    ],
  },

  {
    heading: 'OPERATIONS',
    items: [
      { href: '/support',    label: 'Support Tools', icon: 'ri-customer-service-2-line', badge: 'IN PROGRESS' },
      { href: '/audit-logs', label: 'Audit Logs',    icon: 'ri-file-list-3-line',        badge: 'IN PROGRESS' },
      { href: '/monitoring', label: 'Monitoring',    icon: 'ri-pulse-line',              badge: 'IN PROGRESS' },
    ],
  },

  {
    heading: 'CATALOG',
    items: [
      { href: '/products', label: 'Products', icon: 'ri-apps-line', badge: 'MOCKUP' },
    ],
  },

  {
    heading: 'SYSTEM',
    items: [
      { href: '/settings', label: 'Platform Settings', icon: 'ri-settings-3-line', badge: 'IN PROGRESS' },
    ],
  },
];

/** @deprecated — kept for any existing callers; remove once all are migrated. */
export function buildCCNav() {
  return CC_NAV;
}
