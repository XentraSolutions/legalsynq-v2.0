import { useCallback, useEffect, useState } from 'react';

import {
  BiometricAuthenticationService,
  type BiometricStatus,
} from '@/shared/services/Authentication';

export function useBiometricSettings() {
  const [status, setStatus] = useState<BiometricStatus | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isUpdating, setIsUpdating] = useState(false);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    try {
      setStatus(await BiometricAuthenticationService.getStatus());
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const setEnabled = useCallback(
    async (enabled: boolean) => {
      setIsUpdating(true);
      try {
        if (enabled) {
          await BiometricAuthenticationService.enable();
        } else {
          await BiometricAuthenticationService.disable();
        }
        await refresh();
      } finally {
        setIsUpdating(false);
      }
    },
    [refresh]
  );

  return {
    isLoading,
    isUpdating,
    refresh,
    setEnabled,
    status,
  };
}
