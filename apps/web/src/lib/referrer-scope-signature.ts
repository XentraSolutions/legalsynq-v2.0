import { createHmac } from 'crypto';

const INTERNAL_REQUEST_SECRET =
  process.env['PublicTrustBoundary__InternalRequestSecret'] ??
  process.env.INTERNAL_REQUEST_SECRET ??
  '';

export function signReferrerScope(userId: string, tenantId: string): string {
  if (!INTERNAL_REQUEST_SECRET) return '';

  return createHmac('sha256', INTERNAL_REQUEST_SECRET)
    .update(`${userId}:${tenantId}`)
    .digest('base64');
}
