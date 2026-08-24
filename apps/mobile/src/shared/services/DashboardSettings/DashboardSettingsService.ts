import {
  DASHBOARD_SETTINGS_STORAGE_KEY,
  DEFAULT_DASHBOARD_SETTINGS,
} from '@/shared/constants/dashboardSettings';
import type { DashboardSettings } from '@/shared/types/common';

import { StorageService } from '../Storage';

function normalizeSettings(value: unknown): DashboardSettings {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return DEFAULT_DASHBOARD_SETTINGS;
  }

  const record = value as Record<string, unknown>;
  return {
    useDummyData:
      typeof record.useDummyData === 'boolean'
        ? record.useDummyData
        : DEFAULT_DASHBOARD_SETTINGS.useDummyData,
  };
}

export const DashboardSettingsService = {
  async getSettings(): Promise<DashboardSettings> {
    const value = await StorageService.getItem(DASHBOARD_SETTINGS_STORAGE_KEY);
    if (!value) {
      return DEFAULT_DASHBOARD_SETTINGS;
    }

    try {
      return normalizeSettings(JSON.parse(value));
    } catch {
      return DEFAULT_DASHBOARD_SETTINGS;
    }
  },

  async setSettings(settings: DashboardSettings): Promise<void> {
    await StorageService.setItem(DASHBOARD_SETTINGS_STORAGE_KEY, JSON.stringify(settings));
  },
};
