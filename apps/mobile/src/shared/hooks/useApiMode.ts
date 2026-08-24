import { useCallback, useEffect } from 'react';
import { useAtom } from 'jotai';

import { ApiModeService } from '@/shared/services/ApiMode';
import { apiModeAtom, apiModeHydratedAtom } from '@/shared/state/atoms/apiModeAtom';
import type { ApiMode } from '@/shared/constants/apiMode';

export function useApiMode() {
  const [mode, setModeAtom] = useAtom(apiModeAtom);
  const [hydrated, setHydrated] = useAtom(apiModeHydratedAtom);

  useEffect(() => {
    if (hydrated) {
      return undefined;
    }

    let isMounted = true;

    void ApiModeService.getMode()
      .then((storedMode) => {
        if (isMounted) {
          setModeAtom(storedMode);
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
  }, [hydrated, setHydrated, setModeAtom]);

  const setMode = useCallback(async (nextMode: ApiMode) => {
    await ApiModeService.switchMode(nextMode);
  }, []);

  return {
    hydrated,
    mode,
    setMode,
  };
}
