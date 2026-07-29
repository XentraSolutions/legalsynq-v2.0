import AsyncStorage from '@react-native-async-storage/async-storage';

import {
  DEFAULT_MENU_VISIBILITY,
  MENU_SETTINGS_STORAGE_KEY,
} from '@/shared/constants/menuSettings';

import { MenuSettingsService } from './MenuSettingsService';

describe('MenuSettingsService', () => {
  beforeEach(async () => {
    await AsyncStorage.clear();
    jest.clearAllMocks();
  });

  it('returns the configured defaults when no preferences are stored', async () => {
    await expect(MenuSettingsService.getSettings()).resolves.toEqual(DEFAULT_MENU_VISIBILITY);
  });

  it('persists user visibility choices', async () => {
    const settings = { ...DEFAULT_MENU_VISIBILITY, cases: false, reports: true };

    await MenuSettingsService.setSettings(settings);

    await expect(MenuSettingsService.getSettings()).resolves.toEqual(settings);
  });

  it('uses each item default when stored settings predate a new menu flag', async () => {
    await AsyncStorage.setItem(MENU_SETTINGS_STORAGE_KEY, JSON.stringify({ dashboard: false }));

    await expect(MenuSettingsService.getSettings()).resolves.toEqual({
      ...DEFAULT_MENU_VISIBILITY,
      dashboard: false,
    });
  });
});
