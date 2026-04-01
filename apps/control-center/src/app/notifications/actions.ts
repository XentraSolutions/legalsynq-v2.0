'use server';

import { revalidateTag }        from 'next/cache';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifChannel, NotifBillingRate, NotifBillingPlan } from '@/lib/notifications-api';

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
    const res = await notifClient.post<{ data: { valid: boolean; errors: string[] } }>(
      `/providers/configs/${configId}/validate`, {}
    );
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    if (!res.data.valid) {
      return { success: false, error: res.data.errors.join('; ') };
    }
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Validation failed.' };
  }
}

export async function deleteProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.del(`/providers/configs/${configId}`);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Delete failed.' };
  }
}

export async function testProviderConfig(
  configId: string,
  payload?: { toEmail?: string; subject?: string; body?: string }
): Promise<ActionResult & { message?: string }> {
  await requirePlatformAdmin();
  try {
    const res = await notifClient.post<{ data: { success: boolean; message: string } }>(
      `/providers/configs/${configId}/test`,
      payload ?? {}
    );
    if (!res.data.success) {
      return { success: false, error: res.data.message };
    }
    return { success: true, message: res.data.message };
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

// ── Provider config create / edit ─────────────────────────────────────────────

export interface ProviderConfigCreateInput {
  channel:               NotifChannel;
  providerType:          string;
  displayName:           string;
  credentials?:          Record<string, unknown>;
  senderConfig?:         Record<string, unknown>;
  endpointConfig?:       Record<string, unknown>;
  allowPlatformFallback?:   boolean;
  allowAutomaticFailover?:  boolean;
}

export async function createProviderConfig(
  input: ProviderConfigCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<{ id: string }>('/providers/configs', input);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create provider config.' };
  }
}

export interface ProviderConfigUpdateInput {
  displayName?:          string;
  credentials?:          Record<string, unknown>;
  senderConfig?:         Record<string, unknown>;
  endpointConfig?:       Record<string, unknown>;
  allowPlatformFallback?:   boolean;
  allowAutomaticFailover?:  boolean;
  status?:               'active' | 'inactive';
}

export async function updateProviderConfig(
  id:    string,
  input: ProviderConfigUpdateInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/providers/configs/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update provider config.' };
  }
}

// ── Channel settings update ───────────────────────────────────────────────────

export interface ChannelSettingsInput {
  providerMode?:                   string;
  primaryTenantProviderConfigId?:  string | null;
  fallbackTenantProviderConfigId?: string | null;
  allowPlatformFallback?:          boolean;
  allowAutomaticFailover?:         boolean;
}

export async function updateChannelSettings(
  channel: NotifChannel,
  input:   ChannelSettingsInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.put(`/providers/channel-settings/${channel}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update channel settings.' };
  }
}

// ── Template create ───────────────────────────────────────────────────────────

export interface TemplateCreateInput {
  templateKey:  string;
  channel:      NotifChannel;
  name:         string;
  description?: string | null;
}

export async function createTemplate(
  input: TemplateCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<{ id: string }>('/templates', input);
    revalidateTag(NOTIF_CACHE_TAGS.templates);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create template.' };
  }
}

// ── Template version create ───────────────────────────────────────────────────

export interface TemplateVersionCreateInput {
  bodyTemplate:         string;
  subjectTemplate?:     string | null;
  textTemplate?:        string | null;
  variablesSchemaJson?: Record<string, unknown> | null;
  sampleDataJson?:      Record<string, unknown> | null;
}

export async function createTemplateVersion(
  templateId: string,
  input:      TemplateVersionCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<{ id: string }>(
      `/templates/${templateId}/versions`,
      input,
    );
    revalidateTag(NOTIF_CACHE_TAGS.templates);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create template version.' };
  }
}

// ── Billing plan create / edit ────────────────────────────────────────────────

export interface BillingPlanInput {
  planName:      string;
  billingMode:   'usage_based' | 'flat_rate' | 'hybrid';
  currency:      string;
  effectiveFrom: string;
  effectiveTo?:  string | null;
}

export async function createBillingPlan(
  input: BillingPlanInput,
): Promise<ActionResult<NotifBillingPlan>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<NotifBillingPlan>('/billing/plans', input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create billing plan.' };
  }
}

export interface BillingPlanUpdateInput {
  planName?:      string;
  billingMode?:   string;
  currency?:      string;
  status?:        string;
  effectiveFrom?: string;
  effectiveTo?:   string | null;
}

export async function updateBillingPlan(
  id:    string,
  input: BillingPlanUpdateInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/billing/plans/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update billing plan.' };
  }
}

// ── Billing rate create / edit ────────────────────────────────────────────────

export interface BillingRateInput {
  usageUnit:             string;
  channel?:              NotifChannel | null;
  providerOwnershipMode?: string | null;
  includedQuantity?:     number | null;
  unitPrice?:            number | null;
  isBillable?:           boolean;
}

export async function createBillingRate(
  planId: string,
  input:  BillingRateInput,
): Promise<ActionResult<NotifBillingRate>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<NotifBillingRate>(`/billing/plans/${planId}/rates`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create billing rate.' };
  }
}

export async function updateBillingRate(
  planId: string,
  rateId: string,
  input:  Partial<BillingRateInput>,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/billing/plans/${planId}/rates/${rateId}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update billing rate.' };
  }
}

// ── Contact policy create / edit ──────────────────────────────────────────────

export interface ContactPolicyInput {
  channel?:                    NotifChannel | null;
  blockSuppressedContacts?:    boolean;
  blockUnsubscribedContacts?:  boolean;
  blockComplainedContacts?:    boolean;
  blockBouncedContacts?:       boolean;
  blockInvalidContacts?:       boolean;
  blockCarrierRejectedContacts?: boolean;
  allowManualOverride?:        boolean;
}

export async function createContactPolicy(
  input: ContactPolicyInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post('/contacts/policies', input);
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create contact policy.' };
  }
}

export async function updateContactPolicy(
  id:    string,
  input: ContactPolicyInput & { status?: string },
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/contacts/policies/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update contact policy.' };
  }
}
