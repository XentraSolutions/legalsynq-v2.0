import { apiClient } from '@/shared/api/client';

import { ApplicationsApi } from './endpoints';

describe('ApplicationsApi', () => {
  it('gets an application through the existing Fund gateway route', async () => {
    const application = { id: '01989abc-1234-7000-8000-123456789abc' };
    apiClient.get = jest.fn().mockResolvedValue({ data: application });

    await expect(ApplicationsApi.get(application.id)).resolves.toBe(application);
    expect(apiClient.get).toHaveBeenCalledWith(`/fund/api/applications/${application.id}`);
  });
});
