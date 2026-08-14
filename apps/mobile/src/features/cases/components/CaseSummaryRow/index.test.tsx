import { render } from '@testing-library/react-native';

import { CaseSummaryRow } from './index';

describe('CaseSummaryRow', () => {
  it('renders values, status badges, and missing-value fallbacks', () => {
    const { getByText, rerender } = render(
      <CaseSummaryRow badgeVariant="success" label="Case Status" value="Pre-demand" />
    );

    expect(getByText('Case Status')).toBeTruthy();
    expect(getByText('Pre-demand')).toBeTruthy();

    rerender(<CaseSummaryRow label="Law Firm" value={null} />);
    expect(getByText('—')).toBeTruthy();
  });
});
