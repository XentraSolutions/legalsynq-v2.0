import { apiClient } from '@/shared/api/client';
import { CasesApi } from './endpoints';

describe('CasesApi contact reassignment', () => {
  it('sends the legacy-compatible batch reassignment payload through the gateway', async () => {
    apiClient.post = jest.fn(() =>
      Promise.resolve({ data: { isSuccess: true, message: 'Successfully Reassigned Cases.' } })
    );

    await expect(
      CasesApi.batchReassignContact({
        contactType: '1',
        oldId: 'old-law-firm',
        newId: 'new-law-firm',
      })
    ).resolves.toMatchObject({ isSuccess: true });

    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/cases/batch-reassign', {
      contactType: '1',
      oldId: 'old-law-firm',
      newId: 'new-law-firm',
    });
  });
});
