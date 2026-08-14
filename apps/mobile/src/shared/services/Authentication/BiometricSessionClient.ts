import { AuthenticationApi } from '@/shared/api/endpoints/Authentication';

import { BiometricSessionUnavailableError, type BiometricSessionClient } from './biometricTypes';

export const unavailableBiometricSessionClient: BiometricSessionClient = {
  isAvailable: () => false,
  refreshSession: async () => {
    throw new BiometricSessionUnavailableError();
  },
  enableBiometrics: async () => {
    throw new BiometricSessionUnavailableError();
  },
  disableBiometrics: async () => {
    throw new BiometricSessionUnavailableError();
  },
};

export const biometricSessionClient: BiometricSessionClient = {
  isAvailable: () => true,
  refreshSession: async (input) => {
    const response = await AuthenticationApi.refreshSession(input);
    return {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
    };
  },
  enableBiometrics: async (deviceSessionId) => {
    await AuthenticationApi.enableBiometrics(deviceSessionId);
  },
  disableBiometrics: async (deviceSessionId) => {
    await AuthenticationApi.disableBiometrics(deviceSessionId);
  },
};
