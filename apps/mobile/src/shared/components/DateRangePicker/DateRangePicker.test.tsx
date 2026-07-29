import { fireEvent, render } from '@testing-library/react-native';

import { DateRangePicker } from './DateRangePicker';

describe('DateRangePicker', () => {
  it('renders the selected date range and opens the picker', async () => {
    const { getByText } = await render(
      <DateRangePicker
        isDark={false}
        value={{ startDate: '01/01/2026', endDate: '01/31/2026' }}
        onChange={jest.fn()}
      />
    );

    fireEvent.press(getByText('01 / 01 / 2026 - 01 / 31 / 2026'));

    expect(getByText('Date range')).toBeTruthy();
    expect(getByText('Selected: 1 Jan, 2026 - 31 Jan, 2026')).toBeTruthy();
  });
});
