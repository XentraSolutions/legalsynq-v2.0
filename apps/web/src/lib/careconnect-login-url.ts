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

export function buildCareConnectLoginUrl(portalHostname?: string | null): string {
  const normalizedHost = (portalHostname ?? '').trim().toLowerCase();
  const query = new URLSearchParams({
    returnTo: CARECONNECT_LOGIN_RETURN_TO,
    reason: CARECONNECT_LOGIN_REASON,
  }).toString();

  if (!normalizedHost) {
    return `/login?${query}`;
  }

  const scheme = isLocalhostStyleHost(normalizedHost) ? 'http' : 'https';
  return `${scheme}://${normalizedHost}/login?${query}`;
}

export function getCareConnectLoginUrlFromEnv(): string {
  return buildCareConnectLoginUrl(process.env.CC_COMMON_PORTAL_HOSTNAME);
}
