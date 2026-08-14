import { useCallback, useEffect, useState } from 'react';

import {
  AuthenticationService,
  BiometricAuthenticationService,
  type BiometricStatus,
} from '@/shared/services/Authentication';

export function useBiometricLogin() {
  const [status, setStatus] = useState<BiometricStatus | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSigningIn, setIsSigningIn] = useState(false);

  const refreshStatus = useCallback(async () => {
    setIsLoading(true);
    try {
      setStatus(await BiometricAuthenticationService.getStatus());
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshStatus();
  }, [refreshStatus]);

  const signIn = useCallback(async () => {
    setIsSigningIn(true);
    try {
      const result = await BiometricAuthenticationService.login();
      await AuthenticationService.establishSession(result.accessToken, result.user);
      return result.user;
    } finally {
      setIsSigningIn(false);
      await refreshStatus();
    }
  }, [refreshStatus]);

  return {
    isLoading,
    isSigningIn,
    refreshStatus,
    signIn,
    status,
  };
}
