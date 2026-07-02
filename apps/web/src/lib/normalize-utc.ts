const UTC_KEY_PATTERN = /Utc$/;
const ISO_DATETIME_WITHOUT_OFFSET_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/;

function normalizeUtcString(key: string, value: string): string {
  if (!UTC_KEY_PATTERN.test(key)) return value;
  if (!ISO_DATETIME_WITHOUT_OFFSET_PATTERN.test(value)) return value;
  return `${value}Z`;
}

export function normalizeUtcTimestamps<T>(value: T): T {
  if (Array.isArray(value)) {
    return value.map(item => normalizeUtcTimestamps(item)) as T;
  }

  if (value && typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>).map(([key, entryValue]) => {
      if (typeof entryValue === 'string') {
        return [key, normalizeUtcString(key, entryValue)];
      }

      return [key, normalizeUtcTimestamps(entryValue)];
    });

    return Object.fromEntries(entries) as T;
  }

  return value;
}
