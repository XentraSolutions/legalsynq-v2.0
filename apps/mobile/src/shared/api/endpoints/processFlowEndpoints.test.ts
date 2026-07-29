import { apiClient } from '@/shared/api/client';

import { BillOfSalesApi } from './BillOfSales';
import { ContactsApi } from './Contacts';
import { DocumentsApi } from './Documents';
import { ReportsApi } from './Reports';
import { ServicingApi } from './Servicing';
import { TasksApi } from './Tasks';
import { UserManagementApi } from './UserManagement';

const document = {
  id: 'document-1',
  tenantId: 'tenant-1',
  productId: 'SYNQLIEN',
  referenceId: 'case-1',
  referenceType: 'Case',
  documentTypeId: 'document-type-1',
  title: 'Settlement statement',
  status: 'ACTIVE',
  mimeType: 'application/pdf',
  fileSizeBytes: 1024,
  versionCount: 1,
  scanStatus: 'CLEAN',
  scanThreats: [],
  isDeleted: false,
  createdAt: '2026-07-30T00:00:00Z',
  createdBy: 'user-1',
  updatedAt: '2026-07-30T00:00:00Z',
  updatedBy: 'user-1',
};

describe('mobile SynqLien process-flow endpoints', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    apiClient.get = jest.fn(() => Promise.resolve({ data: {} }));
    apiClient.post = jest.fn(() => Promise.resolve({ data: {} }));
    apiClient.put = jest.fn(() => Promise.resolve({ data: {} }));
    apiClient.patch = jest.fn(() => Promise.resolve({ data: {} }));
    apiClient.delete = jest.fn(() => Promise.resolve({ data: undefined }));
  });

  it('routes task, servicing, contact, bill-of-sale, report, and user flows through gateway paths', async () => {
    await TasksApi.complete('task-1');
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/tasks/task-1/complete');

    await ServicingApi.updateStatus('servicing-1', 'Completed', 'Resolved');
    expect(apiClient.put).toHaveBeenCalledWith('/liens/api/liens/servicing/servicing-1/status', {
      resolution: 'Resolved',
      status: 'Completed',
    });

    await ContactsApi.listByType('Provider');
    expect(apiClient.get).toHaveBeenCalledWith('/liens/api/liens/contacts/providers');

    await BillOfSalesApi.listByLien('lien-1');
    expect(apiClient.get).toHaveBeenCalledWith(
      '/liens/api/liens/liens/lien-1/bill-of-sales'
    );

    await ReportsApi.run({ config: {}, page: 1 });
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/reports/diy/run', {
      config: {},
      page: 1,
    });

    await UserManagementApi.list();
    expect(apiClient.get).toHaveBeenCalledWith('/identity/api/users');
  });

  it('uses the Documents service and returns its issued download URL', async () => {
    apiClient.get = jest.fn(() =>
      Promise.resolve({ data: { data: [document], limit: 50, offset: 0, total: 1 } })
    );

    await expect(
      DocumentsApi.listDocuments({ productId: 'SYNQLIEN', referenceId: 'case-1' })
    ).resolves.toMatchObject({ total: 1, data: [document] });
    expect(apiClient.get).toHaveBeenCalledWith('/documents/documents', {
      params: { productId: 'SYNQLIEN', referenceId: 'case-1' },
    });

    apiClient.post = jest.fn(() =>
      Promise.resolve({
        data: {
          data: {
            accessToken: 'token',
            redeemUrl: '/documents/access/token',
            expiresInSeconds: 300,
            type: 'download',
          },
        },
      })
    );

    await expect(DocumentsApi.getDocumentDownloadUrl('document-1')).resolves.toBe(
      '/documents/access/token'
    );
    expect(apiClient.post).toHaveBeenCalledWith(
      '/documents/documents/document-1/download-url'
    );
  });
});
