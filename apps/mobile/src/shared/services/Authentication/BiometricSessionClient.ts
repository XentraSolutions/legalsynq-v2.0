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
