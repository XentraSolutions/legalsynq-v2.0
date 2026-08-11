import { apiClient } from '@/shared/api/client';

import { AuthenticationApi } from './endpoints';

jest.mock('@/shared/api/client', () => ({
  apiClient: {
    post: jest.fn(),
  },
}));

const client = apiClient as unknown as { post: ReturnType<typeof jest.fn> };

describe('AuthenticationApi biometric sessions', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('refreshes a device session through the documented gateway endpoint', async () => {
    const response = {
      accessToken: 'access-token',
      accessTokenExpiresAtUtc: '2026-08-11T01:15:00Z',
      refreshToken: 'rotated-refresh-token',
      refreshTokenExpiresAtUtc: '2026-11-09T01:00:00Z',
      deviceSessionId: 'device-session-1',
    };
    client.post.mockResolvedValue({ data: response });

    await expect(
      AuthenticationApi.refreshSession({
        refreshToken: 'refresh-token',
        deviceSessionId: 'device-session-1',
      })
    ).resolves.toEqual(response);
    expect(client.post).toHaveBeenCalledWith('/identity/api/auth/session/refresh', {
      refreshToken: 'refresh-token',
      deviceSessionId: 'device-session-1',
    });
  });

  it('uses the documented biometric toggle endpoints', async () => {
    client.post.mockResolvedValue({ data: undefined });

    await AuthenticationApi.enableBiometrics('device/session');
    await AuthenticationApi.disableBiometrics('device/session');

    expect(client.post).toHaveBeenNthCalledWith(
      1,
      '/identity/api/auth/device-sessions/device%2Fsession/biometric/enable'
    );
    expect(client.post).toHaveBeenNthCalledWith(
      2,
      '/identity/api/auth/device-sessions/device%2Fsession/biometric/disable'
    );
  });
});
