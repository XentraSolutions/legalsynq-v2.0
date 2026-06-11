export type PublicReferralFailureReason =
  | 'missing'
  | 'malformed'
  | 'expired'
  | 'revoked'
  | 'referral_mismatch'
  | 'referral_not_found';

export function mapFailureReasonToInvalidReason(reason?: string | null): string {
  switch (reason) {
    case 'missing':
      return 'missing-token';
    case 'revoked':
      return 'revoked';
    case 'referral_not_found':
      return 'referral-not-found';
    case 'expired':
      return 'expired';
    case 'malformed':
    case 'referral_mismatch':
    default:
      return 'expired-or-invalid';
  }
}

export async function readPublicReferralFailureReason(resp: Response): Promise<string | null> {
  try {
    const data = await resp.json() as { reason?: string };
    return typeof data.reason === 'string' ? data.reason : null;
  } catch {
    return null;
  }
}
