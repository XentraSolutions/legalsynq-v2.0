import { test } from 'node:test';
import assert from 'node:assert/strict';

import { normalizeDocumentTimestamps } from '../documents/documents-timestamps.js';

test('normalizeDocumentTimestamps appends Z to document timestamp fields without offsets', () => {
  const result = normalizeDocumentTimestamps({
    data: {
      createdAt: '2026-07-17T07:35:00',
      updatedAt: '2026-07-17T07:40:00',
      versions: [
        {
          uploadedAt: '2026-07-17T07:35:00',
          scanCompletedAt: '2026-07-17T07:36:00.123',
        },
      ],
    },
  });

  assert.equal(result.data.createdAt, '2026-07-17T07:35:00Z');
  assert.equal(result.data.updatedAt, '2026-07-17T07:40:00Z');
  assert.equal(result.data.versions[0].uploadedAt, '2026-07-17T07:35:00Z');
  assert.equal(result.data.versions[0].scanCompletedAt, '2026-07-17T07:36:00.123Z');
});

test('normalizeDocumentTimestamps preserves offset-aware and non-document datetime fields', () => {
  const result = normalizeDocumentTimestamps({
    createdAt: '2026-07-17T07:35:00Z',
    uploadedAt: '2026-07-17T07:35:00+08:00',
    createdAtUtc: '2026-07-17T07:35:00',
    plainDate: '2026-07-17',
  });

  assert.equal(result.createdAt, '2026-07-17T07:35:00Z');
  assert.equal(result.uploadedAt, '2026-07-17T07:35:00+08:00');
  assert.equal(result.createdAtUtc, '2026-07-17T07:35:00Z');
  assert.equal(result.plainDate, '2026-07-17');
});
