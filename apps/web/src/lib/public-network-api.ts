/**
 * CC2-INT-B07 — Server-side public network API helpers.
 *
 * Used exclusively by Server Components (e.g., /network/page.tsx).
 * These functions call the CareConnect backend directly via the gateway,
 * passing the X-Tenant-Id resolved from the tenant subdomain.
 * No authentication token is required — endpoints are AllowAnonymous.
 */

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

export interface PublicNetworkSummary {
  id:            string;
  name:          string;
  description:   string;
  providerCount: number;
}

export interface PublicProviderItem {
  id:               string;
  name:             string;
  organizationName: string | null;
  phone:            string;
  city:             string;
  state:            string;
  postalCode:       string;
  isActive:         boolean;
  acceptingReferrals: boolean;
  accessStage:      string;
  primaryCategory:  string | null;
}

export interface PublicProviderMarker {
  id:               string;
  name:             string;
  organizationName: string | null;
  city:             string;
  state:            string;
  acceptingReferrals: boolean;
  latitude:         number;
  longitude:        number;
}

export interface PublicNetworkDetail {
  networkId:          string;
  networkName:        string;
  networkDescription: string;
  providers:          PublicProviderItem[];
  markers:            PublicProviderMarker[];
}

export interface ResolvedTenant {
  tenantId:    string;
  tenantCode:  string;
  displayName: string;
}

// ── Tenant resolution ──────────────────────────────────────────────────────

/**
 * Resolves a tenant from a subdomain or tenant code by calling the
 * Identity branding endpoint (anonymous).
 *
 * @param tenantCode - The tenant slug/code extracted from the request subdomain.
 * @returns ResolvedTenant or null when the tenant is not found.
 */
export async function resolveTenantFromCode(
  tenantCode: string,
): Promise<ResolvedTenant | null> {
  const url = `${GATEWAY_URL}/identity/api/tenants/current/branding`;

  let res: Response;
  try {
    res = await fetch(url, {
      headers: { 'X-Tenant-Code': tenantCode },
      cache:   'no-store',
    });
  } catch {
    return null;
  }

  if (!res.ok) return null;

  const data = await res.json();
  if (!data.tenantId) return null;

  return {
    tenantId:    data.tenantId,
    tenantCode:  data.tenantCode,
    displayName: data.displayName,
  };
}

// ── Public network endpoints ───────────────────────────────────────────────

/**
 * Fetches all networks for the given tenant (by GUID).
 */
export async function fetchPublicNetworks(
  tenantId: string,
): Promise<PublicNetworkSummary[]> {
  const url = `${GATEWAY_URL}/careconnect/api/public/network`;

  let res: Response;
  try {
    res = await fetch(url, {
      headers: { 'X-Tenant-Id': tenantId },
      cache:   'no-store',
    });
  } catch {
    return [];
  }

  if (!res.ok) return [];
  return res.json();
}

/**
 * Fetches the combined detail (providers + markers) for a single network.
 */
export async function fetchPublicNetworkDetail(
  tenantId:  string,
  networkId: string,
): Promise<PublicNetworkDetail | null> {
  const url = `${GATEWAY_URL}/careconnect/api/public/network/${networkId}/detail`;

  let res: Response;
  try {
    res = await fetch(url, {
      headers: { 'X-Tenant-Id': tenantId },
      cache:   'no-store',
    });
  } catch {
    return null;
  }

  if (!res.ok) return null;
  return res.json();
}

// ── CC2-INT-B08: Public referral initiation ─────────────────────────────────

export interface PublicReferralRequest {
  providerId:       string;
  senderName:       string;
  senderEmail:      string;
  patientFirstName: string;
  patientLastName:  string;
  patientPhone:     string;
  patientEmail?:    string;
  serviceType?:     string;
  notes?:           string;
}

export interface PublicReferralResponse {
  referralId:    string;
  providerId:    string;
  providerName:  string;
  providerStage: string;
  message:       string;
}

export interface PublicReferralError {
  message: string;
  errors?: Record<string, string>;
}
