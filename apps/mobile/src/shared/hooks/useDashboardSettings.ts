import { useCallback, useEffect } from 'react';
import { useAtom } from 'jotai';

import { DashboardSettingsService } from '@/shared/services/DashboardSettings';
import {
  dashboardSettingsAtom,
  dashboardSettingsHydratedAtom,
} from '@/shared/state/atoms/dashboardSettingsAtom';
import type { DashboardSettings } from '@/shared/types/common';

type DashboardSettingsUpdater =
  | DashboardSettings
  | ((current: DashboardSettings) => DashboardSettings);

export function useDashboardSettings() {
  const [settings, setSettingsAtom] = useAtom(dashboardSettingsAtom);
  const [hydrated, setHydrated] = useAtom(dashboardSettingsHydratedAtom);

  useEffect(() => {
    if (hydrated) {
      return undefined;
    }

    let isMounted = true;

    void DashboardSettingsService.getSettings()
      .then((storedSettings) => {
        if (isMounted) {
          setSettingsAtom(storedSettings);
        }
      })
      .finally(() => {
        if (isMounted) {
          setHydrated(true);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [hydrated, setHydrated, setSettingsAtom]);

  const setSettings = useCallback(
    (updater: DashboardSettingsUpdater) => {
      setSettingsAtom((current) => {
        const next = typeof updater === 'function' ? updater(current) : updater;
        void DashboardSettingsService.setSettings(next).catch(() => undefined);
        return next;
      });
      setHydrated(true);
    },
    [setHydrated, setSettingsAtom]
  );

  const setUseDummyData = useCallback(
    (useDummyData: boolean) => {
      setSettings((current) => ({ ...current, useDummyData }));
    },
    [setSettings]
  );

  return {
    hydrated,
    settings,
    setSettings,
    setUseDummyData,
  };
}
