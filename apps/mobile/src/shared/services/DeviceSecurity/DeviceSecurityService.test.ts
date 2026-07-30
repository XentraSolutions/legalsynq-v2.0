import * as LocalAuthentication from 'expo-local-authentication';

import { DeviceSecurityService } from './DeviceSecurityService';

jest.mock('expo-local-authentication', () => ({
  AuthenticationType: {
    FINGERPRINT: 1,
    FACIAL_RECOGNITION: 2,
    IRIS: 3,
  },
  authenticateAsync: jest.fn(),
  hasHardwareAsync: jest.fn(),
  isEnrolledAsync: jest.fn(),
  supportedAuthenticationTypesAsync: jest.fn(),
}));

const localAuthentication = LocalAuthentication as unknown as {
  authenticateAsync: any;
  hasHardwareAsync: any;
  isEnrolledAsync: any;
  supportedAuthenticationTypesAsync: any;
};

describe('DeviceSecurityService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('does not query enrollment when biometric hardware is unavailable', async () => {
    localAuthentication.hasHardwareAsync.mockResolvedValue(false);

    await expect(DeviceSecurityService.getBiometricCapability()).resolves.toMatchObject({
      hasHardware: false,
      isEnrolled: false,
      canUseBiometrics: false,
      label: 'Biometrics',
    });
    expect(localAuthentication.isEnrolledAsync).not.toHaveBeenCalled();
  });

  it('maps native prompt cancellation without treating it as authentication', async () => {
    localAuthentication.authenticateAsync.mockResolvedValue({
      success: false,
      error: 'user_cancel',
    });

    await expect(DeviceSecurityService.authenticate()).resolves.toEqual({
      status: 'cancelled',
    });
  });
});
