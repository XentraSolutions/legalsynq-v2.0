import type { NavGroup, PlatformSession } from '@/types';
import { ProductRole } from '@/types';

const pr = ProductRole;

/**
 * Derives the sidebar navigation from the session.
 * Rules:
 *  - Groups appear only when the session contains at least one relevant product role.
 *  - Individual items inside a group may also be filtered by role.
 *  - The Admin group appears for TenantAdmin and PlatformAdmin.
 *  - This is a UX convenience only — backend enforces real authorization.
 */
export function buildNavGroups(session: PlatformSession): NavGroup[] {
  const roles = session.productRoles;
  const groups: NavGroup[] = [];

  // ── CareConnect ─────────────────────────────────────────────────────────────
  const ccRoles: string[] = [pr.CareConnectReferrer, pr.CareConnectReceiver];
  if (roles.some(r => ccRoles.includes(r))) {
    groups.push({
      id:    'careconnect',
      label: 'CareConnect',
      icon:  'ri-shield-cross-line',
      items: [
        { href: '/careconnect/referrals',    label: 'Referrals',    icon: 'ri-file-list-3-line' },
        { href: '/careconnect/appointments', label: 'Appointments', icon: 'ri-calendar-event-line' },
        ...(roles.includes(pr.CareConnectReferrer)
          ? [{ href: '/careconnect/providers', label: 'Find Providers', icon: 'ri-hospital-line' }]
          : []),
      ],
    });
  }

  // ── SynqFund ────────────────────────────────────────────────────────────────
  const fundRoles: string[] = [pr.SynqFundReferrer, pr.SynqFundFunder];
  if (roles.some(r => fundRoles.includes(r))) {
    groups.push({
      id:    'fund',
      label: 'SynqFund',
      icon:  'ri-bank-line',
      items: [
        { href: '/fund/applications',     label: 'Applications',     icon: 'ri-file-list-line' },
        ...(roles.includes(pr.SynqFundReferrer)
          ? [{ href: '/fund/applications/new', label: 'New Application', icon: 'ri-add-circle-line' }]
          : []),
      ],
    });
  }

  // ── SynqLien ────────────────────────────────────────────────────────────────
  const lienRoles: string[] = [pr.SynqLienSeller, pr.SynqLienBuyer, pr.SynqLienHolder];
  if (roles.some(r => lienRoles.includes(r))) {
    groups.push({
      id:    'lien',
      label: 'SynqLien',
      icon:  'ri-file-stack-line',
      items: [
        ...(roles.includes(pr.SynqLienBuyer)
          ? [{ href: '/lien/marketplace', label: 'Marketplace', icon: 'ri-shopping-bag-line' }]
          : []),
        ...(roles.includes(pr.SynqLienSeller)
          ? [
              { href: '/lien/my-liens',     label: 'My Liens', icon: 'ri-file-stack-line' },
              { href: '/lien/my-liens/new', label: 'New Lien', icon: 'ri-add-circle-line' },
            ]
          : []),
        ...(roles.includes(pr.SynqLienBuyer) || roles.includes(pr.SynqLienHolder)
          ? [{ href: '/lien/portfolio', label: 'Portfolio', icon: 'ri-briefcase-line' }]
          : []),
      ],
    });
  }

  // ── Administration ───────────────────────────────────────────────────────────
  if (session.isTenantAdmin || session.isPlatformAdmin) {
    groups.push({
      id:    'admin',
      label: 'Administration',
      icon:  'ri-settings-3-line',
      items: [
        { href: '/admin/users',         label: 'Users',         icon: 'ri-group-line' },
        { href: '/admin/organizations', label: 'Organizations', icon: 'ri-building-line' },
        { href: '/admin/products',      label: 'Products',      icon: 'ri-cube-line' },
        ...(session.isPlatformAdmin
          ? [{ href: '/admin/tenants', label: 'All Tenants', icon: 'ri-global-line' }]
          : []),
      ],
    });
  }

  return groups;
}

// ── Human-readable org type label ────────────────────────────────────────────

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
