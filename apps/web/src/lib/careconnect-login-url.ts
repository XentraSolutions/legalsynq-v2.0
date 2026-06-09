const CARECONNECT_LOGIN_RETURN_TO = '/careconnect/dashboard';
const CARECONNECT_LOGIN_REASON = 'referral-portal';

function isLocalhostStyleHost(hostname: string): boolean {
  const normalized = hostname.trim().toLowerCase();
  return normalized === 'localhost'
    || normalized.endsWith('.localhost')
    || normalized.startsWith('localhost:')
    || normalized.startsWith('127.')
    || normalized === '[::1]'
    || normalized === '::1'
    || normalized.startsWith('[::1]:');
}

const ABSOLUTE_URL_PATTERN = /^[a-z][a-z0-9+.-]*:\/\//i;

export function normalizeCareConnectPortalHost(portalHostname?: string | null): string {
  const rawValue = (portalHostname ?? '').trim();
  if (!rawValue) {
    return '';
  }

  try {
    const parsed = new URL(
      ABSOLUTE_URL_PATTERN.test(rawValue) ? rawValue : `https://${rawValue}`,
    );
    return parsed.host.trim().toLowerCase();
  } catch {
    return rawValue
      .replace(/^\/+|\/+$/g, '')
      .split('/')[0]
      .trim()
      .toLowerCase();
  }
}

function resolveCareConnectPortalOrigin(portalHostname?: string | null): string {
  const normalizedHost = normalizeCareConnectPortalHost(portalHostname);
  if (!normalizedHost) {
    return '';
  }

  const rawValue = (portalHostname ?? '').trim();
  let explicitProtocol: string | null = null;
  if (ABSOLUTE_URL_PATTERN.test(rawValue)) {
    try {
      explicitProtocol = new URL(rawValue).protocol.replace(/:$/, '').toLowerCase();
    } catch {
      explicitProtocol = null;
    }
  }
  const scheme = explicitProtocol ?? (isLocalhostStyleHost(normalizedHost) ? 'http' : 'https');

  return `${scheme}://${normalizedHost}`;
}

export function buildCareConnectPortalLoginUrl(
  portalHostname?: string | null,
  query?: URLSearchParams | Record<string, string>,
): string {
  const portalOrigin = resolveCareConnectPortalOrigin(portalHostname);
  const queryString = query ? new URLSearchParams(query).toString() : '';
  const loginPath = queryString ? `/login?${queryString}` : '/login';

  if (!portalOrigin) {
    return loginPath;
  }

  return `${portalOrigin}${loginPath}`;
}

export function buildCareConnectLoginUrl(portalHostname?: string | null): string {
  return buildCareConnectPortalLoginUrl(portalHostname, {
    returnTo: CARECONNECT_LOGIN_RETURN_TO,
    reason: CARECONNECT_LOGIN_REASON,
  });
}

export function getCareConnectLoginUrlFromEnv(): string {
  return buildCareConnectLoginUrl(process.env.CC_COMMON_PORTAL_HOSTNAME);
}
