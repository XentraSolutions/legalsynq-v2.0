import { ANALYTICS_EVENTS } from '@/shared/constants/analyticsEvents';
import { AnalyticsService } from '@/shared/services/Analytics';
import { DeviceSecurityService } from '@/shared/services/DeviceSecurity';
import { ApiError } from '@/shared/types/api';

import { BiometricAuthenticationService } from './BiometricAuthenticationService';
import { BiometricCredentialService } from './BiometricCredentialService';
import { BiometricPreferenceService } from './BiometricPreferenceService';
import {
  BiometricAuthenticationCancelledError,
  type BiometricEnrollmentCredentials,
  type BiometricSessionClient,
} from './biometricTypes';

const user = {
  id: 'user-1',
  email: 'user@example.com',
  firstName: 'Test',
  lastName: 'User',
  roles: [],
  permissions: [],
  organization: {
    id: 'organization-1',
    name: 'Test Tenant',
    tenantId: 'tenant-1',
  },
  tenantId: 'tenant-1',
};

const enrollment: BiometricEnrollmentCredentials = {
  accountLabel: user.email,
  deviceSessionId: 'device-session-1',
  refreshToken: 'refresh-token',
  tenantId: user.tenantId,
  user,
};

const capability = {
  hasHardware: true,
  isEnrolled: true,
  supportedTypes: [],
  canUseBiometrics: true,
  label: 'Biometrics' as const,
};

function createClient(overrides: Partial<BiometricSessionClient> = {}): BiometricSessionClient {
  return {
    isAvailable: () => true,
    enableBiometrics: jest.fn(async () => undefined),
    disableBiometrics: jest.fn(async () => undefined),
    refreshSession: jest.fn(async () => ({
      accessToken: 'new-access-token',
      refreshToken: 'new-refresh-token',
      user,
    })),
    ...overrides,
  };
}

describe('BiometricAuthenticationService', () => {
  beforeEach(() => {
    jest.spyOn(DeviceSecurityService, 'getBiometricCapability').mockResolvedValue(capability);
    jest.spyOn(DeviceSecurityService, 'authenticate').mockResolvedValue({ status: 'success' });
    jest.spyOn(BiometricCredentialService, 'save').mockResolvedValue();
    jest.spyOn(BiometricCredentialService, 'get').mockResolvedValue('refresh-token');
    jest.spyOn(BiometricCredentialService, 'remove').mockResolvedValue();
    jest.spyOn(BiometricPreferenceService, 'get').mockResolvedValue(null);
    jest.spyOn(BiometricPreferenceService, 'set').mockResolvedValue();
    jest.spyOn(BiometricPreferenceService, 'clear').mockResolvedValue();
    jest.spyOn(AnalyticsService, 'track').mockImplementation(() => undefined);
  });

  afterEach(async () => {
    BiometricAuthenticationService.resetSessionClient();
    jest.restoreAllMocks();
  });

  it('does not offer enrollment before a backend session client is available', async () => {
    const offer = await BiometricAuthenticationService.prepareEnrollment(enrollment);

    expect(offer.shouldOffer).toBe(false);
    expect(DeviceSecurityService.getBiometricCapability).not.toHaveBeenCalled();
  });

  it('protects the refresh token before marking enrollment as enabled', async () => {
    const client = createClient();
    BiometricAuthenticationService.configureSessionClient(client);
    await BiometricAuthenticationService.prepareEnrollment(enrollment);

    await BiometricAuthenticationService.enable();

    expect(BiometricCredentialService.save).toHaveBeenCalledWith('refresh-token');
    expect(client.enableBiometrics).toHaveBeenCalledWith('device-session-1');
    expect(BiometricPreferenceService.set).toHaveBeenCalledWith({
      enabled: true,
      accountLabel: user.email,
      deviceSessionId: 'device-session-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      user,
    });
    expect(AnalyticsService.track).toHaveBeenCalledWith(
      ANALYTICS_EVENTS.BIOMETRIC_ENROLLMENT_COMPLETED
    );
  });

  it('preserves the protected token for a network failure', async () => {
    const error = new ApiError({ code: 'NETWORK_ERROR', message: 'Network unavailable' });
    const client = createClient({
      refreshSession: jest.fn(async () => {
        throw error;
      }),
    });
    BiometricAuthenticationService.configureSessionClient(client);
    jest.spyOn(BiometricPreferenceService, 'get').mockResolvedValue({
      enabled: true,
      accountLabel: user.email,
      deviceSessionId: 'device-session-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      user,
    });

    await expect(BiometricAuthenticationService.login()).rejects.toBe(error);

    expect(BiometricCredentialService.remove).not.toHaveBeenCalled();
    expect(BiometricPreferenceService.clear).not.toHaveBeenCalled();
  });

  it('treats a cancelled secure-storage prompt as a non-destructive cancellation', async () => {
    const client = createClient();
    BiometricAuthenticationService.configureSessionClient(client);
    jest
      .spyOn(BiometricCredentialService, 'get')
      .mockRejectedValue(new Error('User canceled authentication'));
    jest.spyOn(BiometricPreferenceService, 'get').mockResolvedValue({
      enabled: true,
      accountLabel: 'u***@example.com',
      deviceSessionId: 'device-session-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      user,
    });

    await expect(BiometricAuthenticationService.login()).rejects.toBeInstanceOf(
      BiometricAuthenticationCancelledError
    );

    expect(client.refreshSession).not.toHaveBeenCalled();
    expect(BiometricCredentialService.remove).not.toHaveBeenCalled();
    expect(BiometricPreferenceService.clear).not.toHaveBeenCalled();
  });

  it('clears the protected token when the backend proves the session is revoked', async () => {
    const error = new ApiError({
      code: 'REFRESH_TOKEN_REVOKED',
      message: 'Session revoked',
    });
    const client = createClient({
      refreshSession: jest.fn(async () => {
        throw error;
      }),
    });
    BiometricAuthenticationService.configureSessionClient(client);
    jest.spyOn(BiometricPreferenceService, 'get').mockResolvedValue({
      enabled: true,
      accountLabel: user.email,
      deviceSessionId: 'device-session-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      user,
    });

    await expect(BiometricAuthenticationService.login()).rejects.toThrow(
      'Your saved session is no longer available'
    );

    expect(BiometricCredentialService.remove).toHaveBeenCalled();
    expect(BiometricPreferenceService.clear).toHaveBeenCalled();
  });

  it('clears inconsistent state when a rotated token cannot be stored', async () => {
    const client = createClient();
    BiometricAuthenticationService.configureSessionClient(client);
    jest.spyOn(BiometricPreferenceService, 'get').mockResolvedValue({
      enabled: true,
      accountLabel: 'u***@example.com',
      deviceSessionId: 'device-session-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      user,
    });
    jest
      .spyOn(BiometricCredentialService, 'save')
      .mockRejectedValue(new Error('Secure storage failed'));

    await expect(BiometricAuthenticationService.login()).rejects.toThrow(
      'Your saved login could not be updated'
    );

    expect(BiometricCredentialService.remove).toHaveBeenCalled();
    expect(BiometricPreferenceService.clear).toHaveBeenCalled();
  });
});
