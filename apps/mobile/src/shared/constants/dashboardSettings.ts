import type { DashboardSettings } from '@/shared/types/common';

export const DASHBOARD_SETTINGS_STORAGE_KEY = 'legalsynq.dashboard.settings';

export const DEFAULT_DASHBOARD_SETTINGS: DashboardSettings = {
  useDummyData: false,
};
