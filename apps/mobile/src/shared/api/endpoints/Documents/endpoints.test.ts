import { apiClient } from '@/shared/api/client';

import { DocumentsApi } from './endpoints';

const uploadResponse = {
  data: {
    id: '01989abc-1234-7000-8000-123456789abc',
    tenantId: '11111111-1111-1111-1111-111111111111',
    productId: 'SYNQ_LIENS',
    referenceId: '22222222-2222-2222-2222-222222222222',
    referenceType: 'LIEN',
    documentTypeId: '33333333-3333-3333-3333-333333333333',
    title: 'Medical Record',
    description: 'Medical record supporting the lien',
    mimeType: 'application/pdf',
    fileSizeBytes: 245760,
    currentVersionId: null,
    versionCount: 0,
    scanStatus: 'PENDING',
    scanCompletedAt: null,
    scanThreats: [],
    isDeleted: false,
    retainUntil: null,
    legalHoldAt: null,
    createdAt: '2026-08-04T10:30:00+08:00',
    createdBy: '44444444-4444-4444-4444-444444444444',
    updatedAt: '2026-08-04T10:30:00+08:00',
    updatedBy: '44444444-4444-4444-4444-444444444444',
  },
};

describe('DocumentsApi', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    apiClient.post = jest.fn(() => Promise.resolve({ data: uploadResponse }));
  });

  it('uploads multipart form data through the documents endpoint', async () => {
    const formData = new FormData();
    formData.append('referenceType', 'LIEN');

    await expect(DocumentsApi.uploadDocument(formData)).resolves.toMatchObject({
      id: uploadResponse.data.id,
      referenceType: 'LIEN',
      scanStatus: 'PENDING',
      status: 'ACTIVE',
    });
    expect(apiClient.post).toHaveBeenCalledWith('/documents/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  });
});
