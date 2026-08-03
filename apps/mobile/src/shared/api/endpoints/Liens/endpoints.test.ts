import { apiClient } from '@/shared/api/client';

import { LiensApi } from './endpoints';

describe('LiensApi management endpoints', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    apiClient.get = jest.fn(() => Promise.resolve({ data: { items: [], totalCount: 0 } }));
    apiClient.post = jest.fn(() => Promise.resolve({ data: {} }));
    apiClient.put = jest.fn(() => Promise.resolve({ data: {} }));
    apiClient.delete = jest.fn(() => Promise.resolve({ data: undefined }));
  });

  it('uses the full Liens gateway path for list and details', async () => {
    await LiensApi.listManagementLiens({ page: 1, pageSize: 5 });
    expect(apiClient.get).toHaveBeenCalledWith('/liens/api/liens/liens', {
      params: { page: 1, pageSize: 5 },
    });

    await LiensApi.getManagementLienDetails('lien-1');
    expect(apiClient.post).toHaveBeenCalledWith(
      '/liens/api/liens/liens/details/lien-1',
      {}
    );
  });

  it('uses existing endpoints for confirmed create and update orchestration', async () => {
    await LiensApi.createMedicalInfo({ id: 'lien-1', caseId: 'case-1' });
    expect(apiClient.post).toHaveBeenCalledWith(
      '/liens/api/liens/cases/liens/medical',
      { id: 'lien-1', caseId: 'case-1' }
    );

    await LiensApi.updateFacilityInfo({ liensId: 'lien-1', facilityId: 'facility-1' });
    expect(apiClient.post).toHaveBeenCalledWith(
      '/liens/api/liens/cases/liens/update-facility',
      { liensId: 'lien-1', facilityId: 'facility-1' }
    );
  });

  it('loads active lien document types from the lookup endpoint', async () => {
    apiClient.get = jest.fn(() =>
      Promise.resolve({
        data: [
          { id: 'active', code: 'MedicalRecord', name: 'Medical Record', isActive: true },
          { id: 'inactive', code: 'Old', name: 'Old Type', isActive: false },
        ],
      })
    );

    await expect(LiensApi.listDocumentTypes()).resolves.toEqual([
      { id: 'active', code: 'MedicalRecord', name: 'Medical Record', isActive: true },
    ]);
    expect(apiClient.get).toHaveBeenCalledWith('/liens/lookup/document/type');
  });

  it('extracts the CSV file from the existing export envelope', async () => {
    apiClient.post = jest.fn(() =>
      Promise.resolve({
        data: {
          isSuccess: true,
          data: [{ base64: 'Y3N2', filename: 'liens.csv', export_format: 'csv' }],
        },
      })
    );

    await expect(LiensApi.exportLiens({ lienStatusId: 'Open' })).resolves.toMatchObject({
      filename: 'liens.csv',
    });
    expect(apiClient.post).toHaveBeenCalledWith(
      '/liens/api/liens/cases/liens/generate-csv',
      { lienStatusId: 'Open' }
    );
  });
});
