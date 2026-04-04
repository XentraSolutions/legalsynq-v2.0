'use server';

import { requireOrg } from '@/lib/auth-guards';
import { notificationsServerApi } from '@/lib/notifications-server-api';
import type { BrandedPreviewResult } from '@/lib/notifications-shared';
import { PRODUCT_TYPES, type ProductType } from '@/lib/notifications-shared';

export type ActionResult<T = void> =
  | { success: true; data: T }
  | { success: false; error: string };

export async function previewTemplateVersion(
  templateId: string,
  versionId: string,
  productType: string,
  templateData: Record<string, unknown>,
): Promise<ActionResult<BrandedPreviewResult>> {
  if (!PRODUCT_TYPES.includes(productType as ProductType)) {
    return { success: false, error: 'Invalid product type.' };
  }

  const session = await requireOrg();
  try {
    const tplRes = await notificationsServerApi.globalTemplateGet(session.tenantId, templateId);
    if (tplRes.data.productType !== productType) {
      return { success: false, error: 'Template does not belong to the specified product.' };
    }

    const res = await notificationsServerApi.globalTemplatePreview(
      session.tenantId,
      templateId,
      versionId,
      { tenantId: session.tenantId, productType, templateData },
    );
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Preview failed.' };
  }
}
