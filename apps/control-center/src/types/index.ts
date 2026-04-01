// ── Platform constants ────────────────────────────────────────────────────────
// Mirror of BuildingBlocks.Authorization — keep in sync with backend

export const SystemRole = {
  PlatformAdmin: 'PlatformAdmin',
  TenantAdmin:   'TenantAdmin',
  StandardUser:  'StandardUser',
} as const;
export type SystemRoleValue = typeof SystemRole[keyof typeof SystemRole];

export const ProductRole = {
  CareConnectReferrer:     'CARECONNECT_REFERRER',
  CareConnectReceiver:     'CARECONNECT_RECEIVER',
  SynqFundReferrer:        'SYNQFUND_REFERRER',
  SynqFundFunder:          'SYNQFUND_FUNDER',
  SynqFundApplicantPortal: 'SYNQFUND_APPLICANT_PORTAL',
  SynqLienSeller:          'SYNQLIEN_SELLER',
  SynqLienBuyer:           'SYNQLIEN_BUYER',
  SynqLienHolder:          'SYNQLIEN_HOLDER',
} as const;
export type ProductRoleValue = typeof ProductRole[keyof typeof ProductRole];

export const OrgType = {
  Internal:  'INTERNAL',
  LawFirm:   'LAW_FIRM',
  Provider:  'PROVIDER',
  Funder:    'FUNDER',
  LienOwner: 'LIEN_OWNER',
} as const;
export type OrgTypeValue = typeof OrgType[keyof typeof OrgType];

// ── Session ───────────────────────────────────────────────────────────────────

/**
 * The authoritative Control Center session.
 * Populated from GET /identity/api/auth/me — never from raw JWT decode.
 */
export interface PlatformSession {
  userId:       string;
  email:        string;
  tenantId:     string;
  tenantCode:   string;
  orgId?:       string;
  orgType?:     OrgTypeValue;
  orgName?:     string;
  productRoles: ProductRoleValue[];
  systemRoles:  SystemRoleValue[];
  isPlatformAdmin:  boolean;
  isTenantAdmin:    boolean;
  hasOrg:           boolean;
  avatarDocumentId?:     string;
  expiresAt:             Date;
  sessionTimeoutMinutes: number;
}

// ── Navigation ────────────────────────────────────────────────────────────────

export interface NavItem {
  href:   string;
  label:  string;
  icon?:  string;
  /** Optional status badge displayed next to the nav label in expanded mode. */
  badge?: 'LIVE' | 'MOCKUP' | 'IN PROGRESS';
}

/** A labelled section of nav items — mirrors the web app NavSection. */
export interface NavSection {
  heading?: string;
  items: NavItem[];
}

/** @deprecated Use NavSection[] */
export interface NavGroup {
  id:    string;
  label: string;
  items: NavItem[];
}
