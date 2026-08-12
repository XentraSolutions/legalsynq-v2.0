'use server';

import { createHmac, timingSafeEqual } from 'crypto';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
const IDENTITY_INTERNAL_URL =
  process.env.IDENTITY_INTERNAL_URL ??
  process.env.IDENTITY_SERVICE_URL ??
  'http://127.0.0.1:5001';
const INTERNAL_REQUEST_SECRET =
  process.env['PublicTrustBoundary__InternalRequestSecret'] ??
  process.env.INTERNAL_REQUEST_SECRET ??
  '';

function signTenantId(tenantId: string): string {
  if (!INTERNAL_REQUEST_SECRET) return '';
  return createHmac('sha256', INTERNAL_REQUEST_SECRET).update(tenantId).digest('base64');
}

function publicHeaders(tenantId: string): Record<string, string> {
  const sig = signTenantId(tenantId);
  return {
    'Content-Type': 'application/json',
    'X-Tenant-Id': tenantId,
    ...(sig ? { 'X-Tenant-Id-Sig': sig } : {}),
  };
}

export interface EnrollmentPrefill {
  providerId:   string;
  companyName:  string;
  companyType:  string;
  email:        string;
  phone:        string;
  title?:       string;
  firstName?:   string;
  lastName?:    string;
  addressLine1: string;
  city:         string;
  state:        string;
  postalCode:   string;
}

export interface EnrollmentPrefillResult {
  status: 'ok' | 'already_enrolled' | 'not_found';
  data: EnrollmentPrefill | null;
}

export async function fetchEnrollmentPrefill(
  providerId: string,
  tenantId: string,
): Promise<EnrollmentPrefillResult> {
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/prefill/${providerId}`,
      { headers: publicHeaders(tenantId), cache: 'no-store' },
    );
    if (res.status === 409) return { status: 'already_enrolled', data: null };
    if (!res.ok) return { status: 'not_found', data: null };
    return { status: 'ok', data: await res.json() as EnrollmentPrefill };
  } catch {
    return { status: 'not_found', data: null };
  }
}

export interface SendOtpResult {
  ok:    boolean;
  error?: string;
}

export async function sendOtp(
  email: string,
  providerId: string,
  tenantId: string,
): Promise<SendOtpResult> {
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/send-otp`,
      {
        method:  'POST',
        headers: publicHeaders(tenantId),
        body:    JSON.stringify({ email, providerId }),
      },
    );
    if (!res.ok) {
      const body = await res.json().catch(() => ({})) as { message?: string };
      return { ok: false, error: body.message ?? 'Failed to send verification code.' };
    }
    return { ok: true };
  } catch {
    return { ok: false, error: 'Network error. Please try again.' };
  }
}

export interface RegisterResult {
  ok:    boolean;
  error?: string;
}

export interface RegisterPayload {
  providerId:   string;
  companyName:  string;
  email:        string;
  password:     string;
  title?:       string;
  firstName:    string;
  lastName?:    string;
  phone?:       string;
  addressLine1?: string;
  city?:        string;
  state?:       string;
  postalCode?:  string;
  addressSelectionToken?: string;
  otpCode?:     string;
  tenantId:     string;
}

export interface FirmRegisterPayload {
  tenantId:     string;
  companyName:  string;
  email:        string;
  password:     string;
  title?:       string;
  firstName:    string;
  lastName?:    string;
  phone?:       string;
  addressLine1?: string;
  city?:        string;
  state?:       string;
  postalCode?:  string;
  addressSelectionToken?: string;
}

export async function registerFirmEnrollment(payload: FirmRegisterPayload): Promise<RegisterResult> {
  const { tenantId, ...rest } = payload;
  const body = { ...rest, tenantId };
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/register-firm`,
      {
        method:  'POST',
        headers: publicHeaders(tenantId),
        body:    JSON.stringify(body),
      },
    );
    if (!res.ok) {
      const data = await res.json().catch(() => ({})) as { message?: string; detail?: string; error?: string };
      return { ok: false, error: data.message ?? data.detail ?? data.error ?? 'Registration failed. Please try again.' };
    }
    return { ok: true };
  } catch {
    return { ok: false, error: 'Network error. Please try again.' };
  }
}

export async function registerEnrollment(payload: RegisterPayload): Promise<RegisterResult> {
  const { tenantId, ...body } = payload;
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/register`,
      {
        method:  'POST',
        headers: publicHeaders(tenantId),
        body:    JSON.stringify(body),
      },
    );
    if (!res.ok) {
      const data = await res.json().catch(() => ({})) as { message?: string };
      return { ok: false, error: data.message ?? 'Registration failed. Please try again.' };
    }
    return { ok: true };
  } catch {
    return { ok: false, error: 'Network error. Please try again.' };
  }
}

// ── Enrollment token ───────────────────────────────────────────────────────────
// Short-lived HMAC-signed token that carries enrollment context in the URL
// so no raw PII (email, phone, tenantId) appears as plain query params.

const TOKEN_TTL_SECONDS = 86400; // 24 hours

export interface EnrollmentClaims {
  tenantId: string;
  email?:   string;
  firm?:    string;
  phone?:   string;
  title?:   string;
  /** @deprecated Use contactFirstName/contactLastName instead — kept for tokens issued before the split. */
  contact?: string;
  contactFirstName?: string;
  contactLastName?:  string;
  exp:      number;
}

export interface ExistingEnrollmentPrefill {
  found:       boolean;
  companyName: string;
  email:       string;
  phone:       string;
  title:       string;
  firstName:   string;
  lastName:    string;
  addressLine1: string;
  city:         string;
  state:        string;
  postalCode:   string;
}

export async function fetchExistingEnrollmentPrefill(
  tenantId: string,
  email: string,
): Promise<ExistingEnrollmentPrefill | null> {
  const provisioningToken = process.env.IdentityService__ProvisioningToken ?? '';

  try {
    const res = await fetch(
      `${IDENTITY_INTERNAL_URL}/api/internal/users/enrollment-prefill?tenantId=${encodeURIComponent(tenantId)}&email=${encodeURIComponent(email)}`,
      {
        cache:   'no-store',
        headers: provisioningToken ? { 'X-Provisioning-Token': provisioningToken } : {},
      },
    );

    if (!res.ok) return null;

    const data = await res.json() as Partial<ExistingEnrollmentPrefill>;
    if (!data.found) return null;

    return {
      found:       true,
      companyName: data.companyName ?? '',
      email:       data.email ?? email,
      phone:       data.phone ?? '',
      title:       data.title ?? '',
      firstName:   data.firstName ?? '',
      lastName:    data.lastName ?? '',
      addressLine1: data.addressLine1 ?? '',
      city:         data.city ?? '',
      state:        data.state ?? '',
      postalCode:   data.postalCode ?? '',
    };
  } catch {
    return null;
  }
}

export type PortalAccessStatus = 'active_in_tenant' | 'existing_user_other_tenant' | 'no_account';

export async function checkPortalAccessStatus(
  tenantId: string,
  email: string,
): Promise<PortalAccessStatus> {
  const provisioningToken = process.env.IdentityService__ProvisioningToken ?? '';
  try {
    const res = await fetch(
      `${IDENTITY_INTERNAL_URL}/api/internal/users/portal-access?tenantId=${encodeURIComponent(tenantId)}&email=${encodeURIComponent(email)}`,
      {
        cache: 'no-store',
        headers: provisioningToken ? { 'X-Provisioning-Token': provisioningToken } : {},
      },
    );
    if (!res.ok) return 'no_account';
    const data = await res.json() as { status?: string };
    const status = data.status;
    if (status === 'active_in_tenant' || status === 'existing_user_other_tenant') return status;
    return 'no_account';
  } catch {
    return 'no_account';
  }
}

/**
 * Creates a short-lived signed enrollment token.
 * Safe to call from both server components and client components (server action).
 */
export async function createEnrollmentToken(
  claims: Omit<EnrollmentClaims, 'exp'>,
): Promise<string> {
  if (!INTERNAL_REQUEST_SECRET) throw new Error('INTERNAL_REQUEST_SECRET not configured.');
  const payload: EnrollmentClaims = {
    ...claims,
    exp: Math.floor(Date.now() / 1000) + TOKEN_TTL_SECONDS,
  };
  const body = Buffer.from(JSON.stringify(payload)).toString('base64url');
  const sig  = createHmac('sha256', INTERNAL_REQUEST_SECRET).update(body).digest('base64url');
  return `${body}.${sig}`;
}

/**
 * Verifies and decodes an enrollment token.
 * Returns null if the signature is invalid or the token has expired.
 * Uses constant-time comparison to prevent timing attacks.
 */
export async function decodeEnrollmentToken(raw: string): Promise<EnrollmentClaims | null> {
  if (!INTERNAL_REQUEST_SECRET) return null;
  try {
    const dot = raw.lastIndexOf('.');
    if (dot === -1) return null;
    const body = raw.slice(0, dot);
    const sig  = raw.slice(dot + 1);

    const expected = createHmac('sha256', INTERNAL_REQUEST_SECRET).update(body).digest('base64url');
    const sigBuf = Buffer.from(sig,      'base64url');
    const expBuf = Buffer.from(expected, 'base64url');
    if (sigBuf.length !== expBuf.length || !timingSafeEqual(sigBuf, expBuf)) return null;

    const claims = JSON.parse(Buffer.from(body, 'base64url').toString('utf8')) as EnrollmentClaims;
    if (typeof claims.exp !== 'number' || claims.exp < Math.floor(Date.now() / 1000)) return null;
    return claims;
  } catch {
    return null;
  }
}
