import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { SecureStorageService } from '@/shared/services/SecureStorage';

import { BiometricCredentialService } from './BiometricCredentialService';

describe('BiometricCredentialService', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('recreates a rotated token instead of updating the authenticated item', async () => {
    const deleteItem = jest.spyOn(SecureStorageService, 'deleteItem').mockResolvedValue();
    const setItem = jest.spyOn(SecureStorageService, 'setItem').mockResolvedValue();

    await BiometricCredentialService.rotate('rotated-refresh-token');

    expect(deleteItem).toHaveBeenCalledWith(STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN);
    expect(setItem.mock.calls[0]?.[0]).toBe(STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN);
    expect(setItem.mock.calls[0]?.[1]).toBe('rotated-refresh-token');
    expect(setItem.mock.calls[0]?.[2]).toMatchObject({ requireAuthentication: true });
    expect(deleteItem.mock.invocationCallOrder[0]).toBeLessThan(
      setItem.mock.invocationCallOrder[0]
    );
  });
});
