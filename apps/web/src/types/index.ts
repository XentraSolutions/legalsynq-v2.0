// ── API Response ─────────────────────────────────────────────────────────────

export interface ApiResponse<T> {
  data: T;
  correlationId: string;
  status: number;
}

// ── Platform constants ────────────────────────────────────────────────────────
// Mirror of BuildingBlocks.Authorization — keep in sync with backend
// LS-COR-AUT-006A: ProductRole values use unified PRODUCT:Role claim format.

export const OrgType = {
  Internal:  'INTERNAL',
  LawFirm:   'LAW_FIRM',
  Provider:  'PROVIDER',
  Funder:    'FUNDER',
  LienOwner: 'LIEN_OWNER',
} as const;
export type OrgTypeValue = typeof OrgType[keyof typeof OrgType];

export const SystemRole = {
  PlatformAdmin: 'PlatformAdmin',
  TenantAdmin:   'TenantAdmin',
  StandardUser:  'StandardUser',
} as const;
export type SystemRoleValue = typeof SystemRole[keyof typeof SystemRole];

export const ProductRole = {
  // CareConnect (product code: SYNQ_CARECONNECT)
  CareConnectReferrer: 'SYNQ_CARECONNECT:CARECONNECT_REFERRER',
  CareConnectReceiver: 'SYNQ_CARECONNECT:CARECONNECT_RECEIVER',
  // SynqFund (product code: SYNQ_FUND)
  SynqFundReferrer:        'SYNQ_FUND:SYNQFUND_REFERRER',
  SynqFundFunder:          'SYNQ_FUND:SYNQFUND_FUNDER',
  SynqFundApplicantPortal: 'SYNQ_FUND:SYNQFUND_APPLICANT_PORTAL',
  // SynqLien (product code: SYNQ_LIENS)
  SynqLienSeller: 'SYNQ_LIENS:SYNQLIEN_SELLER',
  SynqLienBuyer:  'SYNQ_LIENS:SYNQLIEN_BUYER',
  SynqLienHolder: 'SYNQ_LIENS:SYNQLIEN_HOLDER',
} as const;
export type ProductRoleValue = typeof ProductRole[keyof typeof ProductRole];

// ── Session shapes ────────────────────────────────────────────────────────────

/**
 * The authoritative frontend session.
 * Populated from GET /identity/api/auth/me — never from raw browser JWT decode alone.
 * The /auth/me endpoint validates the token server-side and returns a safe envelope.
 */
export interface PlatformSession {
  // Identity
  userId: string;
  email:  string;

  // Tenant
  tenantId:   string;
  tenantCode: string;

  // Organization
  orgId?:    string;
  orgType?:  OrgTypeValue;
  orgName?:  string;

  // Access
  productRoles:   ProductRoleValue[];
  systemRoles:    SystemRoleValue[];
  isPlatformAdmin:  boolean;
  isTenantAdmin:    boolean;
  hasOrg:           boolean;

  // Session
  avatarDocumentId?:     string;
  expiresAt:             Date;
  sessionTimeoutMinutes: number;

  // Products
  enabledProducts?: string[];
}

// ── Navigation ────────────────────────────────────────────────────────────────

export interface NavItem {
  href: string;
  label: string;
  icon?: string;
  badge?: string;
  badgeKey?: string;
  requiredRoles?: ProductRoleValue[];
}

export interface NavSection {
  heading?: string;
  items: NavItem[];
}

/** @deprecated Use NavSection[] */
export interface NavGroup {
  id:    string;
  label: string;
  icon?: string;
  items: NavItem[];
}

export interface TenantBranding {
  tenantId?: string;
  tenantCode?: string;
  displayName?: string;
  primaryColor?: string;
  logoUrl?: string;
  logoDocumentId?: string;
  faviconUrl?: string;
}

// ── CareConnect ───────────────────────────────────────────────────────────────

export type CareConnectUserType = 'Provider' | 'CareConnectReceiver';

export interface ApplicantPortalSession extends Pick<PlatformSession, 'userId' | 'email' | 'tenantId' | 'tenantCode' | 'orgId' | 'orgType'> {
  productRoles: ['SYNQ_FUND:SYNQFUND_APPLICANT_PORTAL'];
}
