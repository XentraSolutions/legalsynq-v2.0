import { fireEvent, render } from '@testing-library/react-native';

import { CaseDetailTabBar } from './index';

describe('CaseDetailTabBar', () => {
  it('renders configurable tabs and reports selection changes', () => {
    const onChange = jest.fn();
    const { getByRole, getByText } = render(
      <CaseDetailTabBar
        activeTab="summary"
        tabs={[
          { id: 'summary', label: 'Summary' },
          { id: 'details', label: 'Details' },
        ]}
        onChange={onChange}
      />
    );

    expect(getByRole('tab', { selected: true })).toBeTruthy();
    fireEvent.press(getByText('Details'));
    expect(onChange).toHaveBeenCalledWith('details');
  });
});
