import { afterEach, describe, expect, test, vi } from 'vitest';
import { documentsApi } from './documents.api';

const uploadParams = {
  file: new File(['%PDF'], 'supporting-document.pdf', { type: 'application/pdf' }),
  tenantId: 'tenant-id',
  productId: 'SYNQ_LIENS',
  referenceId: 'lien-id',
  referenceType: 'Lien',
  documentTypeId: 'supporting-document',
  title: 'supporting-document.pdf',
};

describe('documentsApi.upload', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  test('replaces a generic server error with an actionable message and body correlation reference', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        Response.json(
          {
            message: 'An unexpected error occurred.',
            correlationId: 'corr-upload-500',
          },
          { status: 500 },
        ),
      ),
    );

    await expect(documentsApi.upload(uploadParams)).rejects.toThrow(
      'The document service could not process the upload. Please try again or contact support. Reference: corr-upload-500.',
    );
  });

  test('preserves a specific service error instead of replacing it', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        Response.json(
          { message: 'The uploaded file exceeds the maximum allowed size of 25 MB.' },
          {
            status: 413,
            headers: { 'X-Correlation-Id': 'corr-upload-413' },
          },
        ),
      ),
    );

    await expect(documentsApi.upload(uploadParams)).rejects.toThrow(
      'The uploaded file exceeds the maximum allowed size of 25 MB. Reference: corr-upload-413.',
    );
  });
});
