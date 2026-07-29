import { formatDisplayDate } from './dateUtils';

describe('date utilities', () => {
  it('formats ISO dates for display', () => {
    expect(formatDisplayDate('2026-06-21T09:00:00Z')).toContain('2026');
  });
});
