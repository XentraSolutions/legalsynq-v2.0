import { apiClient } from '@/shared/api/client';

import { BillOfSalesApi } from './BillOfSales';
import { CasesApi } from './Cases';
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

  it('uses case-updates v3 for case detail recent updates', async () => {
    apiClient.post = jest.fn(() =>
      Promise.resolve({
        data: {
          data: [
            {
              id: 'update-1',
              note: 'Case information updated.',
              created: '07/31/2026 09:30 AM',
              updated: '07/31/2026 10:00 AM',
            },
          ],
        },
      })
    );

    const updates = await CasesApi.getCaseUpdates('case-1');

    expect(updates).toHaveLength(1);
    expect(updates[0]).toMatchObject({
      id: 'update-1',
      note: 'Case information updated.',
      createdAt: '07/31/2026 09:30 AM',
      updatedAt: '07/31/2026 10:00 AM',
    });
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/cases/case-updates/v3', {
      caseId: 'case-1',
      page: 1,
      limit: 10,
    });
  });

  it('uses liens-updates v3 for the selected case lien activity', async () => {
    apiClient.post = jest.fn(() =>
      Promise.resolve({
        data: {
          data: [
            {
              id: 'update-1',
              action: 'LienStatus',
              description: 'Lien status updated to Open.',
              timestamp: '08/04/2026 10:30 AM',
            },
          ],
        },
      })
    );

    const updates = await CasesApi.getLienUpdates('case-1');

    expect(updates[0]).toMatchObject({
      title: 'LienStatus',
      updatedAt: '08/04/2026 10:30 AM',
    });
    expect(apiClient.post).toHaveBeenCalledWith('/liens/api/liens/cases/liens-updates/v3', {
      caseId: 'case-1',
      page: 1,
      limit: 100,
    });
  });

  it('uses the manage-case payoff quote, merge, and delete endpoints', async () => {
    apiClient.get = jest.fn(() =>
      Promise.resolve({ data: { url: 'https://documents.example/payoff-quote.pdf' } })
    );

    await expect(CasesApi.getPayoffQuote('case-1')).resolves.toEqual({
      url: 'https://documents.example/payoff-quote.pdf',
    });
    expect(apiClient.get).toHaveBeenCalledWith(
      '/api/lien/api/liens/cases/payoff-quote/case-1'
    );

    await CasesApi.mergeCase('case-1', 'case-2');
    expect(apiClient.post).toHaveBeenCalledWith('/api/lien/api/liens/cases/mergecase', {
      caseIdA: 'case-1',
      caseIdB: 'case-2',
    });

    await CasesApi.deleteCase('case-1');
    expect(apiClient.delete).toHaveBeenCalledWith('/api/lien/api/liens/cases/delete/case-1');
  });

  it('normalizes categorized case notes and deletes through the case notes endpoint', async () => {
    apiClient.get = jest.fn(() =>
      Promise.resolve({
        data: [
          {
            id: 'note-1',
            caseId: 'case-1',
            content: 'Follow up with the court.',
            category: 'follow-up',
            createdByUserId: 'user-1',
            createdByName: 'John Doe',
            createdAtUtc: '2026-07-24T14:29:00Z',
            isEdited: false,
            isPinned: false,
          },
        ],
      })
    );

    const notes = await CasesApi.getCaseNotes('case-1');
    expect(notes).toHaveLength(1);
    expect(notes[0]).toMatchObject({
      authorId: 'user-1',
      category: 'follow-up',
      content: 'Follow up with the court.',
    });
    expect(apiClient.get).toHaveBeenCalledWith('/liens/api/liens/cases/case-1/notes');

    await CasesApi.deleteCaseNote('case-1', 'note-1');
    expect(apiClient.delete).toHaveBeenCalledWith(
      '/liens/api/liens/cases/case-1/notes/note-1'
    );
  });
});
