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
      icon:  'HeartPulse',
      items: [
        { href: '/careconnect/referrals',    label: 'Referrals' },
        { href: '/careconnect/appointments', label: 'Appointments' },
        ...(roles.includes(pr.CareConnectReferrer)
          ? [{ href: '/careconnect/providers', label: 'Find Providers' }]
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
      icon:  'Banknote',
      items: [
        { href: '/fund/applications', label: 'Applications' },
        ...(roles.includes(pr.SynqFundReferrer)
          ? [{ href: '/fund/applications/new', label: 'New Application' }]
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
      icon:  'FileStack',
      items: [
        ...(roles.includes(pr.SynqLienBuyer)
          ? [{ href: '/lien/marketplace', label: 'Marketplace' }]
          : []),
        ...(roles.includes(pr.SynqLienSeller)
          ? [
              { href: '/lien/my-liens',     label: 'My Liens' },
              { href: '/lien/my-liens/new', label: 'New Lien' },
            ]
          : []),
        ...(roles.includes(pr.SynqLienBuyer) || roles.includes(pr.SynqLienHolder)
          ? [{ href: '/lien/portfolio', label: 'Portfolio' }]
          : []),
      ],
    });
  }

  // ── Administration ───────────────────────────────────────────────────────────
  if (session.isTenantAdmin || session.isPlatformAdmin) {
    groups.push({
      id:    'admin',
      label: 'Administration',
      icon:  'Settings',
      items: [
        { href: '/admin/users',         label: 'Users' },
        { href: '/admin/organizations', label: 'Organizations' },
        { href: '/admin/products',      label: 'Products' },
        ...(session.isPlatformAdmin
          ? [{ href: '/admin/tenants', label: 'All Tenants' }]
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
