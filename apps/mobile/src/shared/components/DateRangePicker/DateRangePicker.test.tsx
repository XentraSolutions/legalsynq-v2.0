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

  it('displays and applies an unbounded All Dates range', async () => {
    const onChange = jest.fn();
    const { getAllByText, getByText } = await render(
      <DateRangePicker
        allowAllDates
        isDark={false}
        value={{ startDate: '', endDate: '' }}
        onChange={onChange}
      />
    );

    fireEvent.press(getByText('All Dates'));
    fireEvent.press(getAllByText('All Dates')[1]);

    expect(onChange).toHaveBeenCalledWith({ startDate: '', endDate: '' });
  });
});
