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
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/liens/details/lien-1', {});
  });

  it('loads selling dashboard analytics with its comparison filter', async () => {
    const params = {
      startDate: '2026-01-01',
      endDate: '2026-01-31',
      compare: 'previousPeriod' as const,
    };

    await LiensApi.getSellingDashboardAnalytics(params);

    expect(apiClient.get).toHaveBeenCalledWith('/liens/api/liens/selling/analytics/dashboard', {
      params,
    });
  });

  it('loads the monthly aging report with its as-of date and pagination', async () => {
    const params = { asOfDate: '2026-08-25', page: 1, pageSize: 10 };

    const report = {
      asOfDate: '2026-08-25',
      currency: 'USD',
      summaryTotals: { totalAmount: 4500 },
      data: [{ lienCode: 'SL-1', totalAmount: 4500 }],
    };
    apiClient.get = jest.fn(() => Promise.resolve({ data: report }));

    await expect(LiensApi.getMonthlyAgingReport(params)).resolves.toBe(report);

    expect(apiClient.get).toHaveBeenCalledWith('/liens/api/liens/reports/monthly-aging', {
      params,
    });
  });

  it('unwraps a gateway envelope without unwrapping the monthly report rows', async () => {
    const params = { asOfDate: '2026-08-25', page: 1, pageSize: 10 };
    const report = {
      asOfDate: '2026-08-25',
      currency: 'USD',
      summaryTotals: { totalAmount: 4500 },
      data: [{ lienCode: 'SL-1', totalAmount: 4500 }],
    };
    apiClient.get = jest.fn(() => Promise.resolve({ data: { data: report } }));

    await expect(LiensApi.getMonthlyAgingReport(params)).resolves.toBe(report);
  });

  it('retries the exact dashboard path when the gateway-prefixed route is missing', async () => {
    const params = { compare: 'previousPeriod' as const };
    apiClient.get = jest
      .fn()
      .mockRejectedValueOnce({ statusCode: 404 })
      .mockResolvedValueOnce({ data: { currency: 'USD' } });

    await LiensApi.getSellingDashboardAnalytics(params);

    expect(apiClient.get).toHaveBeenNthCalledWith(
      1,
      '/liens/api/liens/selling/analytics/dashboard',
      { params }
    );
    expect(apiClient.get).toHaveBeenNthCalledWith(2, '/api/liens/selling/analytics/dashboard', {
      params,
    });
  });

  it('requests every lien page from the selected case endpoint', async () => {
    apiClient.post = jest.fn(() => Promise.resolve({ data: { items: [], totalCount: 0 } }));

    await LiensApi.listAllCaseLiens('case-1');

    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/cases/liens/case-1', {
      page: 1,
      limit: 200,
    });
  });

  it('uses existing endpoints for confirmed create and update orchestration', async () => {
    await LiensApi.createMedicalInfo({ id: 'lien-1', caseId: 'case-1' });
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/cases/liens/medical', {
      id: 'lien-1',
      caseId: 'case-1',
    });

    await LiensApi.updateFacilityInfo({ liensId: 'lien-1', facilityId: 'facility-1' });
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/cases/liens/update-facility', {
      liensId: 'lien-1',
      facilityId: 'facility-1',
    });
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
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/cases/liens/generate-csv', {
      lienStatusId: 'Open',
    });
  });
});
