'use server';

import { revalidateTag }        from 'next/cache';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifChannel }    from '@/lib/notifications-api';

// ── Shared result type ────────────────────────────────────────────────────────

export interface ActionResult<T = undefined> {
  success: boolean;
  error?:  string;
  data?:   T;
}

// ── Provider config mutations ─────────────────────────────────────────────────

export async function validateProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/providers/configs/${configId}/validate`, {});
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Validation failed.' };
  }
}

export async function testProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/providers/configs/${configId}/test`, {});
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Test failed.' };
  }
}

export async function activateProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/providers/configs/${configId}/activate`, {});
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Activation failed.' };
  }
}

export async function deactivateProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/providers/configs/${configId}/deactivate`, {});
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Deactivation failed.' };
  }
}

// ── Template mutations ────────────────────────────────────────────────────────

export async function publishTemplateVersion(
  templateId: string,
  versionId:  string,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/templates/${templateId}/versions/${versionId}/publish`, {});
    revalidateTag(NOTIF_CACHE_TAGS.templates);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Publish failed.' };
  }
}

export interface PreviewTemplateResult {
  subject?: string;
  bodyHtml?: string;
  bodyText?: string;
}

export async function previewTemplateVersion(
  templateId: string,
  versionId:  string,
  data:       Record<string, string>,
): Promise<ActionResult<PreviewTemplateResult>> {
  await requirePlatformAdmin();
  try {
    const result = await notifClient.post<PreviewTemplateResult>(
      `/templates/${templateId}/versions/${versionId}/preview`,
      { data },
    );
    return { success: true, data: result };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Preview failed.' };
  }
}

// ── Contact suppression mutations ─────────────────────────────────────────────

export interface AddSuppressionInput {
  channel:         NotifChannel;
  contactValue:    string;
  suppressionType: string;
  reason?:         string;
}

export async function addSuppression(input: AddSuppressionInput): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post('/contacts/suppressions', {
      channel:         input.channel,
      contactValue:    input.contactValue,
      suppressionType: input.suppressionType,
      source:          'manual',
      reason:          input.reason ?? null,
    });
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to add suppression.' };
  }
}

export async function liftSuppression(suppressionId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/contacts/suppressions/${suppressionId}`, { status: 'lifted' });
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to lift suppression.' };
  }
}

// ── Rate-limit policy mutations ───────────────────────────────────────────────

export interface RateLimitInput {
  channel?:      NotifChannel | null;
  limitCount:    number;
  windowSeconds: number;
}

export async function createRateLimitPolicy(input: RateLimitInput): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post('/billing/rate-limits', {
      channel:       input.channel ?? null,
      limitCount:    input.limitCount,
      windowSeconds: input.windowSeconds,
    });
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create rate-limit policy.' };
  }
}

export async function updateRateLimitPolicy(
  id:    string,
  input: Partial<RateLimitInput> & { status?: 'active' | 'inactive' },
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/billing/rate-limits/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update rate-limit policy.' };
  }
}
