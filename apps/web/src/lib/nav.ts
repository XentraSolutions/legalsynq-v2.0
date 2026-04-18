import type { NavSection, PlatformSession, ProductRoleValue } from '@/types';
import { ProductRole } from '@/types';

// ── Per-product sidebar navigation (sections) ─────────────────────────────────

export const PRODUCT_NAV: Record<string, NavSection[]> = {
  careconnect: [
    {
      items: [
        { href: '/careconnect/dashboard',    label: 'Dashboard',    icon: 'ri-dashboard-line' },
        { href: '/careconnect/providers',    label: 'Providers',    icon: 'ri-hospital-line', requiredRoles: [ProductRole.CareConnectReferrer] },
        { href: '/careconnect/referrals',    label: 'Referrals',    icon: 'ri-file-list-3-line', badgeKey: 'newReferrals' },
      ],
    },
  ],

  fund: [
    {
      items: [
        { href: '/fund/dashboard',    label: 'Dashboard',    icon: 'ri-dashboard-line' },
        { href: '/fund/processing',   label: 'Processing',   icon: 'ri-loader-4-line' },
        { href: '/fund/underwriting', label: 'Underwriting', icon: 'ri-file-search-line' },
        { href: '/fund/payouts',      label: 'Payouts',      icon: 'ri-money-dollar-circle-line' },
        { href: '/fund/reports',      label: 'Reports',      icon: 'ri-bar-chart-2-line' },
      ],
    },
  ],

  lien: [
    {
      heading: 'MY TASKS',
      items: [
        { href: '/lien/dashboard',     label: 'Dashboard',     icon: 'ri-dashboard-line' },
        { href: '/lien/task-manager',  label: 'Task Manager',  icon: 'ri-task-line' },
        { href: '/lien/cases',         label: 'Cases',         icon: 'ri-folder-open-line' },
        { href: '/lien/liens',         label: 'Liens',         icon: 'ri-stack-line' },
        { href: '/lien/bill-of-sales', label: 'Bill of Sales', icon: 'ri-receipt-line', sellModeOnly: true },
        { href: '/lien/servicing',     label: 'Servicing',     icon: 'ri-tools-line' },
        { href: '/lien/contacts',      label: 'Contacts',      icon: 'ri-contacts-book-line' },
      ],
    },
    {
      heading: 'MARKETPLACE',
      sellModeOnly: true,
      items: [
        { href: '/lien/my-liens',    label: 'My Liens',    icon: 'ri-price-tag-3-line',         requiredRoles: [ProductRole.SynqLienSeller] },
        { href: '/lien/marketplace', label: 'Marketplace', icon: 'ri-store-2-line',              requiredRoles: [ProductRole.SynqLienBuyer] },
        { href: '/lien/portfolio',   label: 'Portfolio',   icon: 'ri-briefcase-line',            requiredRoles: [ProductRole.SynqLienBuyer, ProductRole.SynqLienHolder] },
      ],
    },
    {
      heading: 'MY TOOLS',
      items: [
        { href: '/lien/batch-entry',       label: 'Batch Entry',       icon: 'ri-upload-2-line', requiredRoles: [ProductRole.SynqLienSeller] },
        { href: '/lien/document-handling', label: 'Document Handling', icon: 'ri-file-copy-2-line' },
      ],
    },
    {
      heading: 'SETTINGS',
      items: [
        { href: '/lien/user-management',      label: 'User Management',   icon: 'ri-user-settings-line' },
        { href: '/lien/settings/workflow',    label: 'Workflow Settings', icon: 'ri-git-branch-line'    },
      ],
    },
  ],

  ai: [
    { items: [{ href: '/ai/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' }] },
  ],

  insights: [
    {
      items: [
        { href: '/insights/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' },
        { href: '/insights/reports',   label: 'Reports',   icon: 'ri-file-chart-line' },
        { href: '/insights/schedules', label: 'Schedules', icon: 'ri-calendar-schedule-line' },
      ],
    },
  ],
};

// ── Product metadata ──────────────────────────────────────────────────────────

export const PRODUCT_META: Record<string, { label: string; icon: string; color: string; iconSrc: string }> = {
  careconnect: { label: 'Synq CareConnect', icon: 'ri-shield-cross-line',  color: '#2563eb', iconSrc: '/product-icons/synqconnect.png' },
  fund:        { label: 'Synq Funds',        icon: 'ri-bank-line',           color: '#16a34a', iconSrc: '/product-icons/synqfund.png'    },
  lien:        { label: 'Synq Liens',        icon: 'ri-stack-line',          color: '#7c3aed', iconSrc: '/product-icons/synqlien.png'    },
  ai:          { label: 'Synq AI',           icon: 'ri-robot-line',          color: '#d97706', iconSrc: '/product-icons/synqai.png'      },
  insights:    { label: 'Synq Insights',     icon: 'ri-bar-chart-2-line',    color: '#0891b2', iconSrc: '/product-icons/synqinsight.png' },
};

/**
 * Maps backend product codes (as returned by auth/me `enabledProducts`) to the
 * PRODUCT_META key used in the tenant portal.
 * Values on the left are the frontend-friendly codes emitted by the Identity service
 * (e.g. "CareConnect", "SynqFund"). Values on the right are PRODUCT_META keys.
 */
export const PRODUCT_CODE_TO_NAV_KEY: Record<string, string> = {
  CareConnect:  'careconnect',
  SynqFund:     'fund',
  SynqLien:     'lien',
  SynqAI:       'ai',
  SynqInsights: 'insights',
  SynqBill:     'bill',
  SynqRx:       'rx',
  SynqPayout:   'payout',
};

/**
 * Converts a list of backend enabledProducts codes into the set of PRODUCT_META
 * keys that should be shown on the dashboard.
 * Falls back to showing ALL products when the list is empty (e.g. during
 * onboarding, or for PlatformAdmin users whose tokens predate this feature).
 */
export function resolveEnabledNavKeys(enabledProducts: string[]): Set<string> {
  if (enabledProducts.length === 0) return new Set(Object.keys(PRODUCT_META));
  const keys = new Set<string>();
  for (const code of enabledProducts) {
    const key = PRODUCT_CODE_TO_NAV_KEY[code];
    if (key && key in PRODUCT_META) keys.add(key);
  }
  return keys;
}

export function filterNavByRoles(sections: NavSection[], userRoles: ProductRoleValue[]): NavSection[] {
  return sections
    .map(section => ({
      ...section,
      items: section.items.filter(item => {
        if (!item.requiredRoles || item.requiredRoles.length === 0) return true;
        return item.requiredRoles.some(role => userRoles.includes(role));
      }),
    }))
    .filter(section => section.items.length > 0);
}

export function filterNavByAccess(
  sections: NavSection[],
  userRoles: ProductRoleValue[],
  isSellMode: boolean,
): NavSection[] {
  return filterNavByRoles(sections, userRoles)
    .filter((s) => !s.sellModeOnly || isSellMode)
    .map((s) => ({
      ...s,
      items: s.items.filter((item) => !item.sellModeOnly || isSellMode),
    }))
    .filter((s) => s.items.length > 0);
}

// ── Infer product from pathname ───────────────────────────────────────────────

export function inferProductFromPath(pathname: string): string | null {
  if (pathname.startsWith('/careconnect')) return 'careconnect';
  if (pathname.startsWith('/fund'))        return 'fund';
  if (pathname.startsWith('/lien'))        return 'lien';
  if (pathname.startsWith('/ai'))          return 'ai';
  if (pathname.startsWith('/insights'))    return 'insights';
  return null;
}

// ── Org type label ────────────────────────────────────────────────────────────

export function orgTypeLabel(orgType: string | undefined): string {
  const labels: Record<string, string> = {
    LAW_FIRM:   'Law Firm',
    PROVIDER:   'Provider',
    FUNDER:     'Funder',
    LIEN_OWNER: 'Lien Owner',
    INTERNAL:   'Internal',
  };
  return orgType ? (labels[orgType] ?? orgType) : 'No Organization';
}

// ── Global bottom nav (always shown at the foot of every product sidebar) ─────

export const GLOBAL_BOTTOM_NAV: NavSection = {
  heading: 'ACCOUNT',
  items: [
    { href: '/my-work',       label: 'My Work',       icon: 'ri-task-line'      },
    { href: '/notifications', label: 'Notifications', icon: 'ri-mail-send-line' },
    { href: '/activity',      label: 'Activity Log',  icon: 'ri-history-line'   },
  ],
};

// ── Admin nav sections (shown when session has admin role) ────────────────────

/**
 * Returns the Administration NavSection[] for sidebar rendering.
 * Returns an empty array for standard users — they see nothing.
 */
export function buildNavGroups(session: PlatformSession): NavSection[] {
  if (!session.isPlatformAdmin && !session.isTenantAdmin) return [];

  const sections: NavSection[] = [];

  sections.push({
    heading: 'AUTHORIZATION',
    items: [
      { href: '/tenant/authorization/users',     label: 'Users',     icon: 'ri-user-line'            },
      { href: '/tenant/authorization/groups',     label: 'Groups',    icon: 'ri-group-line'           },
      { href: '/tenant/authorization/access',     label: 'Access',    icon: 'ri-shield-keyhole-line'  },
      { href: '/tenant/authorization/simulator',  label: 'Simulator', icon: 'ri-test-tube-line'       },
    ],
  });

  sections.push({
    heading: 'ANALYTICS',
    items: [
      { href: '/tenant/analytics', label: 'Operations', icon: 'ri-line-chart-line' },
    ],
  });

  return sections;
}
