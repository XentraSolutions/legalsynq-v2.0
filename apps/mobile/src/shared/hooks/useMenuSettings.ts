import { useCallback, useEffect } from 'react';
import { useAtom } from 'jotai';

import type { MenuVisibilityKey, MenuVisibilitySettings } from '@/shared/constants/menuSettings';
import { MenuSettingsService } from '@/shared/services/MenuSettings';
import {
  menuVisibilityAtom,
  menuVisibilityHydratedAtom,
} from '@/shared/state/atoms/menuSettingsAtom';

type MenuSettingsUpdater =
  | MenuVisibilitySettings
  | ((current: MenuVisibilitySettings) => MenuVisibilitySettings);

export function useMenuSettings() {
  const [settings, setSettingsAtom] = useAtom(menuVisibilityAtom);
  const [hydrated, setHydrated] = useAtom(menuVisibilityHydratedAtom);

  useEffect(() => {
    if (hydrated) return undefined;

    let isMounted = true;
    void MenuSettingsService.getSettings()
      .then((storedSettings) => {
        if (isMounted) setSettingsAtom(storedSettings);
      })
      .finally(() => {
        if (isMounted) setHydrated(true);
      });

    return () => {
      isMounted = false;
    };
  }, [hydrated, setHydrated, setSettingsAtom]);

  const setSettings = useCallback(
    (updater: MenuSettingsUpdater) => {
      setSettingsAtom((current) => {
        const next = typeof updater === 'function' ? updater(current) : updater;
        void MenuSettingsService.setSettings(next).catch(() => undefined);
        return next;
      });
      setHydrated(true);
    },
    [setHydrated, setSettingsAtom]
  );

  const setMenuVisible = useCallback(
    (key: MenuVisibilityKey, visible: boolean) => {
      setSettings((current) => ({ ...current, [key]: visible }));
    },
    [setSettings]
  );

  const setMenuGroupVisible = useCallback(
    (keys: readonly MenuVisibilityKey[], visible: boolean) => {
      setSettings((current) => {
        const next = { ...current };
        keys.forEach((key) => {
          next[key] = visible;
        });
        return next;
      });
    },
    [setSettings]
  );

  return { hydrated, settings, setMenuGroupVisible, setMenuVisible, setSettings };
}
