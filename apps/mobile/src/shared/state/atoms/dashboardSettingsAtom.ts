import { atom } from 'jotai';

import { DEFAULT_DASHBOARD_SETTINGS } from '@/shared/constants/dashboardSettings';
import type { DashboardSettings } from '@/shared/types/common';

export const dashboardSettingsAtom = atom<DashboardSettings>(DEFAULT_DASHBOARD_SETTINGS);

export const dashboardSettingsHydratedAtom = atom(false);
