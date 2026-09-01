import { test } from 'node:test';
import assert from 'node:assert/strict';

import { dateConverter, dateConvertertoIso } from '../cases/cases.mapper.js';
import { formatLegacyDateOnly } from '../format-date.js';

test('formatLegacyDateOnly does not timezone-shift legacy calendar dates', () => {
  assert.equal(
    formatLegacyDateOnly('08/28/2026', 'America/Los_Angeles'),
    '08/28/2026',
  );
});

test('case date converters preserve calendar dates in both API formats', () => {
  assert.equal(dateConverter('2026-08-28'), '08/28/2026');
  assert.equal(dateConvertertoIso('08/28/2026'), '2026-08-28');
  assert.equal(dateConvertertoIso('2026-08-28'), '2026-08-28');
});
