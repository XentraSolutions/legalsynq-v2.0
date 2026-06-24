import * as LocalAuthentication from 'expo-local-authentication';

export const DeviceSecurityService = {
  async isBiometricAvailable(): Promise<boolean> {
    const [hasHardware, isEnrolled] = await Promise.all([
      LocalAuthentication.hasHardwareAsync(),
      LocalAuthentication.isEnrolledAsync(),
    ]);

    return hasHardware && isEnrolled;
  },

  async authenticate(promptMessage = 'Unlock LegalSynq'): Promise<boolean> {
    const result = await LocalAuthentication.authenticateAsync({
      promptMessage,
      cancelLabel: 'Cancel',
    });

    return result.success;
  },
};
