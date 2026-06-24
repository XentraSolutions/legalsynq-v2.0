import { formatCurrency, maskPatientName, titleCase } from './index';

describe('formatting utilities', () => {
  it('formats currency without cents', () => {
    expect(formatCurrency(125000)).toBe('$125,000');
  });

  it('converts underscored text to title case', () => {
    expect(titleCase('AUTO_ACCIDENT')).toBe('Auto Accident');
  });

  it('handles empty title case values', () => {
    expect(titleCase('')).toBe('');
  });

  it('masks patient last names', () => {
    expect(maskPatientName('John Doe')).toBe('John D.');
  });

  it('leaves single patient names readable', () => {
    expect(maskPatientName('John')).toBe('John');
  });
});
