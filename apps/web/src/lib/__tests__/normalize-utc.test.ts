import { test } from 'node:test';
import assert from 'node:assert/strict';

import { normalizeUtcTimestamps } from '../normalize-utc.js';

test('normalizeUtcTimestamps appends Z to UTC-keyed datetime strings without offsets', () => {
  const result = normalizeUtcTimestamps({
    createdAtUtc: '2026-06-16T09:00:00',
    nested: {
      scheduledAtUtc: '2026-06-16T10:30:00.123',
    },
    items: [
      { updatedAtUtc: '2026-06-16T11:45:00' },
    ],
  });

  assert.equal(result.createdAtUtc, '2026-06-16T09:00:00Z');
  assert.equal(result.nested.scheduledAtUtc, '2026-06-16T10:30:00.123Z');
  assert.equal(result.items[0].updatedAtUtc, '2026-06-16T11:45:00Z');
});

test('normalizeUtcTimestamps leaves offset-aware and non-UTC fields unchanged', () => {
  const result = normalizeUtcTimestamps({
    createdAtUtc: '2026-06-16T09:00:00Z',
    sentAtUtc: '2026-06-16T09:00:00+01:00',
    clientDob: '2026-06-01T00:00:00',
    plainDate: '2026-06-16',
  });

  assert.equal(result.createdAtUtc, '2026-06-16T09:00:00Z');
  assert.equal(result.sentAtUtc, '2026-06-16T09:00:00+01:00');
  assert.equal(result.clientDob, '2026-06-01T00:00:00');
  assert.equal(result.plainDate, '2026-06-16');
});
