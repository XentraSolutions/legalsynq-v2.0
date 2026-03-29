'use server';

import { controlCenterServerApi } from '@/lib/control-center-api';
import type { PlatformSetting } from '@/types/control-center';

export interface UpdateSettingResult {
  success:  boolean;
  setting?: PlatformSetting;
  error?:   string;
}

/**
 * Server Action: update a single platform setting by key.
 *
 * Called from PlatformSettingsPanel (client component).
 * Uses the mock API stub; wire to real endpoint by updating
 * controlCenterServerApi.settings.update.
 */
export async function updateSetting(
  key:   string,
  value: string | number | boolean,
): Promise<UpdateSettingResult> {
  try {
    const setting = await controlCenterServerApi.settings.update(key, value);
    return { success: true, setting };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to update setting.',
    };
  }
}
