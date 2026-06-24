const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

export interface CareConnectAccessCodeStatus {
  configured: boolean;
  version: number;
}

export interface CareConnectAccessCodeVerifyResult {
  ok: boolean;
  configured: boolean;
  version: number;
}

export async function resolveTenantIdFromPublicHost(host: string): Promise<string | null> {
  const parts = host.split('.');
  const subdomain = parts.length >= 3 ? parts[0] : null;

  if (subdomain) {
    const byCode = await tryResolve(`/tenant/api/v1/public/resolve/by-code/${encodeURIComponent(subdomain)}`);
    if (byCode) return byCode;

    const bySubdomain = await tryResolve(`/tenant/api/v1/public/resolve/by-subdomain/${encodeURIComponent(subdomain)}`);
    if (bySubdomain) return bySubdomain;
  }

  if (process.env.NODE_ENV === 'development' && !subdomain) {
    const devCode = process.env.NEXT_PUBLIC_TENANT_CODE;
    if (devCode) {
      const byCode = await tryResolve(`/tenant/api/v1/public/resolve/by-code/${encodeURIComponent(devCode)}`);
      if (byCode) return byCode;

      const bySubdomain = await tryResolve(`/tenant/api/v1/public/resolve/by-subdomain/${encodeURIComponent(devCode)}`);
      if (bySubdomain) return bySubdomain;
    }
  }

  return null;
}

export async function fetchPublicAccessCodeStatus(tenantId: string): Promise<CareConnectAccessCodeStatus> {
  const res = await fetch(
    `${GATEWAY_URL}/tenant/api/v1/public/tenants/${encodeURIComponent(tenantId)}/careconnect/public-network/access-code/status`,
    { method: 'GET' },
  );

  if (!res.ok) {
    throw new Error(`Access-code status request failed with ${res.status}.`);
  }

  return res.json() as Promise<CareConnectAccessCodeStatus>;
}

export async function verifyPublicAccessCode(
  tenantId: string,
  code: string,
): Promise<CareConnectAccessCodeVerifyResult> {
  const res = await fetch(
    `${GATEWAY_URL}/tenant/api/v1/public/tenants/${encodeURIComponent(tenantId)}/careconnect/public-network/access-code/verify`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code }),
    },
  );

  if (!res.ok) {
    throw new Error(`Access-code verify request failed with ${res.status}.`);
  }

  return res.json() as Promise<CareConnectAccessCodeVerifyResult>;
}

async function tryResolve(path: string): Promise<string | null> {
  try {
    const res = await fetch(`${GATEWAY_URL}${path}`, { method: 'GET' });
    if (!res.ok) return null;

    const body = await res.json() as { tenantId?: string };
    return body.tenantId && body.tenantId !== '' ? body.tenantId : null;
  } catch {
    return null;
  }
}
