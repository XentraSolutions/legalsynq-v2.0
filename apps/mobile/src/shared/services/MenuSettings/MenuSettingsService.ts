import {
  DEFAULT_MENU_VISIBILITY,
  MENU_SETTINGS_STORAGE_KEY,
  MENU_VISIBILITY_OPTIONS,
  type MenuVisibilitySettings,
} from '@/shared/constants/menuSettings';

import { StorageService } from '../Storage';

function normalizeSettings(value: unknown): MenuVisibilitySettings {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return { ...DEFAULT_MENU_VISIBILITY };
  }

  const record = value as Record<string, unknown>;
  return Object.fromEntries(
    MENU_VISIBILITY_OPTIONS.map(({ key }) => [
      key,
      typeof record[key] === 'boolean' ? record[key] : DEFAULT_MENU_VISIBILITY[key],
    ])
  ) as MenuVisibilitySettings;
}

export const MenuSettingsService = {
  async getSettings(): Promise<MenuVisibilitySettings> {
    const value = await StorageService.getItem(MENU_SETTINGS_STORAGE_KEY);
    if (!value) return { ...DEFAULT_MENU_VISIBILITY };

    try {
      return normalizeSettings(JSON.parse(value));
    } catch {
      return { ...DEFAULT_MENU_VISIBILITY };
    }
  },

  async setSettings(settings: MenuVisibilitySettings): Promise<void> {
    await StorageService.setItem(MENU_SETTINGS_STORAGE_KEY, JSON.stringify(settings));
  },
};
