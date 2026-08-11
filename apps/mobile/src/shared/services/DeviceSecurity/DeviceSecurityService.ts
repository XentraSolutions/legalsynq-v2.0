import * as LocalAuthentication from 'expo-local-authentication';
import { Platform } from 'react-native';

export type BiometricLabel = 'Face ID' | 'Touch ID' | 'Fingerprint' | 'Biometrics';

export interface BiometricCapability {
  hasHardware: boolean;
  isEnrolled: boolean;
  supportedTypes: LocalAuthentication.AuthenticationType[];
  canUseBiometrics: boolean;
  label: BiometricLabel;
}

export type DeviceAuthenticationResult =
  | { status: 'success' }
  | { status: 'cancelled' }
  | { status: 'locked_out'; message?: string }
  | { status: 'unavailable'; message?: string }
  | { status: 'failed'; message?: string };

function biometricLabel(supportedTypes: LocalAuthentication.AuthenticationType[]): BiometricLabel {
  const supportsFace = supportedTypes.includes(
    LocalAuthentication.AuthenticationType.FACIAL_RECOGNITION
  );
  const supportsFingerprint = supportedTypes.includes(
    LocalAuthentication.AuthenticationType.FINGERPRINT
  );

  if (Platform.OS === 'ios' && supportsFace) return 'Face ID';
  if (Platform.OS === 'ios' && supportsFingerprint) return 'Touch ID';
  if (Platform.OS === 'android' && supportsFingerprint && !supportsFace) return 'Fingerprint';
  return 'Biometrics';
}

function authenticationStatus(error?: string): DeviceAuthenticationResult {
  if (error === 'user_cancel' || error === 'system_cancel' || error === 'app_cancel') {
    return { status: 'cancelled' };
  }
  if (error === 'lockout') {
    return { status: 'locked_out', message: 'Biometric authentication is temporarily locked.' };
  }
  if (error === 'not_available' || error === 'not_enrolled' || error === 'passcode_not_set') {
    return { status: 'unavailable', message: 'Biometric authentication is unavailable.' };
  }
  return { status: 'failed', message: 'Biometric authentication was unsuccessful.' };
}

export const DeviceSecurityService = {
  async getBiometricCapability(): Promise<BiometricCapability> {
    const hasHardware = await LocalAuthentication.hasHardwareAsync();
    const [isEnrolled, supportedTypes] = await Promise.all([
      hasHardware ? LocalAuthentication.isEnrolledAsync() : Promise.resolve(false),
      hasHardware
        ? LocalAuthentication.supportedAuthenticationTypesAsync()
        : Promise.resolve([] as LocalAuthentication.AuthenticationType[]),
    ]);

    return {
      hasHardware,
      isEnrolled,
      supportedTypes,
      canUseBiometrics: hasHardware && isEnrolled,
      label: biometricLabel(supportedTypes),
    };
  },

  async isBiometricAvailable(): Promise<boolean> {
    return (await DeviceSecurityService.getBiometricCapability()).canUseBiometrics;
  },

  async authenticate(promptMessage = 'Unlock LegalSynq'): Promise<DeviceAuthenticationResult> {
    const result = await LocalAuthentication.authenticateAsync({
      promptMessage,
      cancelLabel: 'Cancel',
      disableDeviceFallback: false,
      fallbackLabel: 'Use device passcode',
    });

    return result.success ? { status: 'success' } : authenticationStatus(result.error);
  },
};
