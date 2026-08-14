import { apiClient } from '@/shared/api/client';
import { FacilitiesApi } from './endpoints';

describe('FacilitiesApi', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    apiClient.get = jest.fn(() => Promise.resolve({ data: { items: [], totalCount: 0 } }));
    apiClient.post = jest.fn(() => Promise.resolve({ data: { id: 'facility-1' } }));
    apiClient.put = jest.fn(() => Promise.resolve({ data: { id: 'facility-1' } }));
    apiClient.delete = jest.fn(() => Promise.resolve({ data: undefined }));
  });

  it('uses the dedicated facility list instead of the contacts endpoint', async () => {
    await FacilitiesApi.list({ isActive: true, page: 1, pageSize: 5, search: 'clinic' });
    expect(apiClient.get).toHaveBeenCalledWith('/liens/api/liens/facilities', {
      params: { isActive: true, page: 1, pageSize: 5, search: 'clinic' },
    });
  });

  it('uses nested facility staff endpoints', async () => {
    apiClient.get = jest.fn(() => Promise.resolve({ data: [] }));
    await FacilitiesApi.listStaff('facility-1');
    expect(apiClient.get).toHaveBeenCalledWith(
      '/liens/api/liens/facilities/facility-1/contact-persons'
    );

    await FacilitiesApi.createStaff('facility-1', { firstName: 'Ava', lastName: 'Reed' });
    expect(apiClient.post).toHaveBeenCalledWith(
      '/liens/api/liens/facilities/facility-1/contact-persons',
      { firstName: 'Ava', lastName: 'Reed' }
    );
  });
});
