import type { NavSection, PlatformSession } from '@/types';

// ── Per-product sidebar navigation (sections) ─────────────────────────────────

export const PRODUCT_NAV: Record<string, NavSection[]> = {
  careconnect: [
    {
      items: [
        { href: '/careconnect/dashboard',    label: 'Dashboard',    icon: 'ri-dashboard-line' },
        { href: '/careconnect/providers',    label: 'Providers',    icon: 'ri-hospital-line' },
        { href: '/careconnect/referrals',    label: 'Referrals',    icon: 'ri-file-list-3-line' },
        { href: '/careconnect/appointments', label: 'Appointments', icon: 'ri-calendar-2-line' },
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
        { href: '/lien/bill-of-sales', label: 'Bill of Sales', icon: 'ri-receipt-line' },
        { href: '/lien/servicing',     label: 'Servicing',     icon: 'ri-tools-line' },
        { href: '/lien/contacts',      label: 'Contacts',      icon: 'ri-contacts-book-line' },
      ],
    },
    {
      heading: 'MY TOOLS',
      items: [
        { href: '/lien/batch-entry',       label: 'Batch Entry',       icon: 'ri-upload-2-line' },
        { href: '/lien/document-handling', label: 'Document Handling', icon: 'ri-file-copy-2-line' },
      ],
    },
    {
      heading: 'SETTINGS',
      items: [
        { href: '/lien/user-management', label: 'User Management', icon: 'ri-user-settings-line' },
      ],
    },
  ],

  ai: [
    { items: [{ href: '/ai/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' }] },
  ],

  insights: [
    { items: [{ href: '/insights/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' }] },
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
    { href: '/notifications', label: 'Notifications', icon: 'ri-mail-send-line' },
    { href: '/activity',      label: 'Activity Log',  icon: 'ri-history-line'   },
  ],
};

// ── Legacy stub ───────────────────────────────────────────────────────────────

export function buildNavGroups(_session: PlatformSession) {
  return [];
}
