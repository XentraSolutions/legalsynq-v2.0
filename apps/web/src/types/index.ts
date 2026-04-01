// ── Platform constants ────────────────────────────────────────────────────────
// Mirror of BuildingBlocks.Authorization — keep in sync with backend

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
  // CareConnect
  CareConnectReferrer: 'CARECONNECT_REFERRER',
  CareConnectReceiver: 'CARECONNECT_RECEIVER',
  // SynqFund
  SynqFundReferrer:        'SYNQFUND_REFERRER',
  SynqFundFunder:          'SYNQFUND_FUNDER',
  SynqFundApplicantPortal: 'SYNQFUND_APPLICANT_PORTAL',
  // SynqLien
  SynqLienSeller: 'SYNQLIEN_SELLER',
  SynqLienBuyer:  'SYNQLIEN_BUYER',
  SynqLienHolder: 'SYNQLIEN_HOLDER',
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

  // Organization (absent if user has no org membership)
  orgId?:    string;
  orgType?:  OrgTypeValue;
  orgName?:  string;

  // Access control (display-only; backend enforces all real authorization)
  productRoles:   ProductRoleValue[];
  systemRoles:    SystemRoleValue[];

  // Tenant-level product entitlements — which products the tenant has licensed.
  // Populated from TenantProduct.IsEnabled at auth/me time.
  // Used to filter the dashboard product tiles. Values e.g. "CareConnect", "SynqFund".
  enabledProducts: string[];

  // Convenience flags — derived during session decode, not from JWT trust
  isPlatformAdmin: boolean;
  isTenantAdmin:   boolean;
  hasOrg:          boolean;

  // Profile picture — references a document in the Documents service
  avatarDocumentId?: string;

  // Token lifecycle
  expiresAt: Date;

  // Per-tenant idle session timeout in minutes (default 30 if not configured)
  sessionTimeoutMinutes: number;
}

/**
 * Session shape for the injured party portal.
 * Uses a separate HttpOnly cookie (portal_session).
 * No orgId / orgType — parties are not org members.
 */
export interface PartySession {
  partyId:      string;
  email?:       string;
  tenantId:     string;
  productRoles: ['SYNQFUND_APPLICANT_PORTAL'];
  expiresAt:    Date;
}

// ── Tenant branding ───────────────────────────────────────────────────────────

export interface TenantBranding {
  tenantId:        string;
  tenantCode:      string;
  displayName:     string;
  logoUrl?:        string;
  logoDocumentId?: string;
  primaryColor?:   string;  // hex, injected as --color-primary CSS variable
  faviconUrl?:     string;
}

// ── Navigation ────────────────────────────────────────────────────────────────

export interface NavItem {
  href:  string;
  label: string;
  icon?: string;
}

/** A labelled group of items within a single product's sidebar. */
export interface NavSection {
  heading?: string;  // e.g. "MY TASKS" — omit for an unlabelled section
  items: NavItem[];
}

export interface NavGroup {
  id:    string;
  label: string;
  icon:  string;
  items: NavItem[];
}

// ── API layer ─────────────────────────────────────────────────────────────────

export interface ApiResponse<T> {
  data:          T;
  correlationId: string;
  status:        number;
}
