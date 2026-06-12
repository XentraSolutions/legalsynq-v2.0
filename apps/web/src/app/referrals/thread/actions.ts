'use server';

import { fetchPublicCareConnect } from '../lib/public-referral-proxy';

export interface TokenActionResult {
  success: boolean;
  error?:  string;
  status?: string;
}

export async function acceptReferralByToken(
  referralId: string,
  token:       string,
): Promise<TokenActionResult> {
  try {
    const resp = await fetchPublicCareConnect(`/api/referrals/${referralId}/accept-by-token`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ token }),
    });
    if (resp.status === 409) return { success: false, error: 'This referral has already been responded to.' };
    if (!resp.ok) {
      const body = await resp.json().catch(() => ({}));
      return { success: false, error: (body as { detail?: string }).detail ?? 'Could not accept the referral. Please try again.' };
    }
    const data = await resp.json();
    return { success: true, status: (data as { status?: string }).status };
  } catch {
    return { success: false, error: 'Network error. Please check your connection and try again.' };
  }
}

export async function declineReferralByToken(
  referralId: string,
  token:       string,
): Promise<TokenActionResult> {
  try {
    const resp = await fetchPublicCareConnect(`/api/referrals/${referralId}/decline-by-token`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ token }),
    });
    if (resp.status === 409) return { success: false, error: 'This referral has already been responded to.' };
    if (!resp.ok) {
      const body = await resp.json().catch(() => ({}));
      return { success: false, error: (body as { detail?: string }).detail ?? 'Could not decline the referral. Please try again.' };
    }
    const data = await resp.json();
    return { success: true, status: (data as { status?: string }).status };
  } catch {
    return { success: false, error: 'Network error. Please check your connection and try again.' };
  }
}

export interface UpdateTreatmentTypeResult {
  success: boolean;
  error?:  string;
}

export async function updateTreatmentTypeByToken(
  referralId:      string,
  token:           string,
  treatmentTypeId: string | null,
): Promise<UpdateTreatmentTypeResult> {
  try {
    const resp = await fetchPublicCareConnect(
      `/api/referrals/${referralId}/treatment-type-by-token`,
      {
        method:  'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ token, treatmentTypeId }),
      },
    );
    if (resp.status === 409) return { success: false, error: 'This referral cannot be updated in its current status.' };
    if (!resp.ok) {
      const body = await resp.json().catch(() => ({}));
      return { success: false, error: (body as { detail?: string }).detail ?? 'Could not update treatment type. Please try again.' };
    }
    return { success: true };
  } catch {
    return { success: false, error: 'Network error. Please check your connection and try again.' };
  }
}

export interface PostCommentResult {
  success: boolean;
  error?: string;
  comment?: {
    id: string;
    senderType: string;
    senderName: string;
    message: string;
    createdAt: string;
  };
}

export async function postComment(
  token: string,
  senderType: string,
  senderName: string,
  message: string,
): Promise<PostCommentResult> {
  if (!senderType || (senderType !== 'referrer' && senderType !== 'provider')) {
    return { success: false, error: 'Please select your role.' };
  }
  if (!senderName?.trim()) {
    return { success: false, error: 'Please enter your name.' };
  }
  if (!message?.trim() || message.length > 4000) {
    return { success: false, error: 'Message is required and must be under 4000 characters.' };
  }

  try {
    const resp = await fetchPublicCareConnect(`/api/public/referrals/thread/comments?token=${encodeURIComponent(token)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ senderType, senderName: senderName.trim(), message: message.trim() }),
    });

    if (!resp.ok) {
      const body = await resp.json().catch(() => ({}));
      return { success: false, error: (body as { error?: string }).error ?? 'Failed to send message. Please try again.' };
    }

    const comment = await resp.json();
    return { success: true, comment };
  } catch {
    return { success: false, error: 'Network error. Please check your connection and try again.' };
  }
}
