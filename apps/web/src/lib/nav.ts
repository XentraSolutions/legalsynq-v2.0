import type { NavItem, PlatformSession } from '@/types';

// ── Per-product sidebar navigation ────────────────────────────────────────────
// Keyed by product id (matches ALL_PRODUCTS ids in top-bar.tsx)

export const PRODUCT_NAV: Record<string, NavItem[]> = {
  careconnect: [
    { href: '/careconnect/dashboard',  label: 'Dashboard',  icon: 'ri-dashboard-line' },
    { href: '/careconnect/providers',  label: 'Providers',  icon: 'ri-hospital-line' },
    { href: '/careconnect/referrals',  label: 'Referrals',  icon: 'ri-file-list-3-line' },
  ],
  fund: [
    { href: '/fund/dashboard',     label: 'Dashboard',    icon: 'ri-dashboard-line' },
    { href: '/fund/processing',    label: 'Processing',   icon: 'ri-loader-4-line' },
    { href: '/fund/underwriting',  label: 'Underwriting', icon: 'ri-file-search-line' },
    { href: '/fund/payouts',       label: 'Payouts',      icon: 'ri-money-dollar-circle-line' },
    { href: '/fund/reports',       label: 'Reports',      icon: 'ri-bar-chart-2-line' },
  ],
  lien: [
    { href: '/lien/dashboard',      label: 'Dashboard',    icon: 'ri-dashboard-line' },
    { href: '/lien/cases',          label: 'Cases',        icon: 'ri-folder-open-line' },
    { href: '/lien/liens',          label: 'Liens',        icon: 'ri-file-stack-line' },
    { href: '/lien/bill-of-sales',  label: 'Bill of Sales', icon: 'ri-receipt-line' },
    { href: '/lien/contacts',       label: 'Contacts',     icon: 'ri-contacts-book-line' },
  ],
  ai: [
    { href: '/ai/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' },
  ],
  insights: [
    { href: '/insights/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' },
  ],
};

// ── Product metadata (label + icon) used in the sidebar header ───────────────

export const PRODUCT_META: Record<string, { label: string; icon: string; color: string }> = {
  careconnect: { label: 'Synq CareConnect', icon: 'ri-shield-cross-line',  color: '#2563eb' },
  fund:        { label: 'Synq Funds',        icon: 'ri-bank-line',           color: '#16a34a' },
  lien:        { label: 'Synq Liens',        icon: 'ri-file-stack-line',     color: '#7c3aed' },
  ai:          { label: 'Synq AI',           icon: 'ri-robot-line',          color: '#d97706' },
  insights:    { label: 'Synq Insights',     icon: 'ri-bar-chart-2-line',    color: '#0891b2' },
};

// ── Infer product id from pathname ────────────────────────────────────────────

export function inferProductFromPath(pathname: string): string | null {
  if (pathname.startsWith('/careconnect')) return 'careconnect';
  if (pathname.startsWith('/fund'))        return 'fund';
  if (pathname.startsWith('/lien'))        return 'lien';
  if (pathname.startsWith('/ai'))          return 'ai';
  if (pathname.startsWith('/insights'))    return 'insights';
  return null;
}

// ── Human-readable org type label ─────────────────────────────────────────────

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

// ── Legacy helper (kept so existing imports don't break) ──────────────────────

export function buildNavGroups(_session: PlatformSession) {
  return [];
}
