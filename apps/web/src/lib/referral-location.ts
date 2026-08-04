/** Fields shared by every referral location shape (tenant/common portal + public link pages). */
export interface ReferralLocationFields {
  locationAddressLine1?: string | null;
  locationCity?:         string | null;
  locationState?:        string | null;
  locationPostalCode?:   string | null;
}

/**
 * Formats a referral's resolved location (facility-first, provider-fallback — see the
 * backend's ReferralLocationResolver) as "Address, City, State ZIP", skipping any missing
 * parts. Returns null when there's no address to show.
 */
export function formatReferralLocation(referral: ReferralLocationFields): string | null {
  const addressLine = [referral.locationAddressLine1, referral.locationCity, referral.locationState]
    .filter(Boolean)
    .join(', ');
  const withZip = [addressLine, referral.locationPostalCode].filter(Boolean).join(' ').trim();

  return withZip || null;
}
