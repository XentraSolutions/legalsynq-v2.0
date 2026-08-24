import { normalizeUtcTimestamps } from '../normalize-utc';

const DOCUMENT_UTC_KEYS = new Set([
  'createdAt',
  'updatedAt',
  'uploadedAt',
  'scanCompletedAt',
  'deletedAt',
]);

const ISO_DATETIME_WITHOUT_OFFSET_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/;

export function normalizeDocumentTimestamps<T>(value: T): T {
  const normalized = normalizeUtcTimestamps(value);

  if (Array.isArray(normalized)) {
    return normalized.map(item => normalizeDocumentTimestamps(item)) as T;
  }

  if (normalized && typeof normalized === 'object') {
    const entries = Object.entries(normalized as Record<string, unknown>).map(([key, entryValue]) => {
      if (typeof entryValue === 'string' &&
          DOCUMENT_UTC_KEYS.has(key) &&
          ISO_DATETIME_WITHOUT_OFFSET_PATTERN.test(entryValue)) {
        return [key, `${entryValue}Z`];
      }

      return [key, normalizeDocumentTimestamps(entryValue)];
    });

    return Object.fromEntries(entries) as T;
  }

  return normalized;
}
