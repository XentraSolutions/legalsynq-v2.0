'use server';

import { fetchPublicCareConnect } from '../lib/public-referral-proxy';

export interface AutoProvisionResult {
  success:      boolean;
  alreadyActive?: boolean;
  loginUrl?:    string | null;
  error?:       string;
}

export async function autoProvision(
  referralId:         string,
  token:              string,
  requesterFirstName: string,
  requesterLastName:  string,
  requesterEmail:     string,
): Promise<AutoProvisionResult> {
  try {
    const resp = await fetchPublicCareConnect(`/api/referrals/${referralId}/auto-provision`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ token, requesterFirstName, requesterLastName, requesterEmail }),
    });

    if (!resp.ok) {
      return { success: false, error: 'Something went wrong. Please try again or contact the referring party.' };
    }

    const data = await resp.json() as {
      success?:      boolean;
      alreadyActive?: boolean;
      loginUrl?:     string | null;
    };

    return {
      success:      data.success ?? false,
      alreadyActive: data.alreadyActive,
      loginUrl:     data.loginUrl ?? null,
    };
  } catch {
    return { success: false, error: 'Connection error. Please check your internet connection and try again.' };
  }
}
