import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, test, expect, vi } from 'vitest';
import { DatePicker } from './date-picker';

// The masked input is keystroke-driven, so typing must go through
// userEvent.type/keyboard character-by-character rather than fireEvent
// with a whole string — that's what actually exercises @react-input/mask.
async function typeDigits(user: ReturnType<typeof userEvent.setup>, digits: string) {
  await user.keyboard(digits);
}

function ControlledDatePicker(props: Omit<Parameters<typeof DatePicker>[0], 'value' | 'onChange'>) {
  const [value, setValue] = useState<string | undefined>(undefined);
  return <DatePicker value={value} onChange={setValue} {...props} />;
}

describe('DatePicker', () => {
  test('shows the placeholder when no value is set', () => {
    render(<DatePicker value={undefined} onChange={() => {}} placeholder="Pick a date" />);
    expect(screen.getByPlaceholderText('Pick a date')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Pick a date')).toHaveValue('');
  });

  test('displays an existing value formatted as MM/DD/YYYY', () => {
    render(<DatePicker value="2024-03-05" onChange={() => {}} />);
    expect(screen.getByDisplayValue('03/05/2024')).toBeInTheDocument();
  });

  test('typing a full valid date reports it in YYYY-MM-DD and reflects it in the calendar', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DatePicker value={undefined} onChange={onChange} />);

    await user.click(screen.getByRole('textbox'));
    await typeDigits(user, '03052024');

    expect(onChange).toHaveBeenLastCalledWith('2024-03-05');
    // The month/year selects should have jumped to the typed date.
    const [monthSelect, yearSelect] = screen.getAllByRole('combobox');
    expect(monthSelect).toHaveValue('2'); // March, zero-indexed
    expect(yearSelect).toHaveValue('2024');
  });

  test('clicking a day in the calendar selects it, fires onChange, and closes the popover', async () => {
    const user = userEvent.setup();
    render(<ControlledDatePicker />);

    await user.click(screen.getByRole('textbox'));
    const grid = await screen.findByRole('grid');
    expect(grid).toBeInTheDocument();

    // The 15th of the currently-displayed month.
    await user.click(screen.getByRole('button', { name: /15/ }));

    await waitFor(() => expect(screen.queryByRole('grid')).not.toBeInTheDocument());
    const today = new Date();
    const expected = `${String(today.getMonth() + 1).padStart(2, '0')}/15/${today.getFullYear()}`;
    expect(screen.getByRole('textbox')).toHaveValue(expected);
  });

  test('clear button empties the value and refocuses the input', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DatePicker value="2024-03-05" onChange={onChange} />);

    // There are two buttons (calendar icon + clear "x"); the clear button
    // only renders once a value/text is present, so grab the last one.
    const buttons = screen.getAllByRole('button');
    await user.click(buttons[buttons.length - 1]);

    expect(onChange).toHaveBeenCalledWith('');
    expect(screen.getByRole('textbox')).toHaveValue('');
    expect(screen.getByRole('textbox')).toHaveFocus();
  });

  test('clearable=false hides the clear button even with a value', () => {
    render(<DatePicker value="2024-03-05" onChange={() => {}} clearable={false} />);
    // Only the calendar-icon button should be present.
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });

  test('disabled disables the input and suppresses the clear button', () => {
    render(<DatePicker value="2024-03-05" onChange={() => {}} disabled />);
    expect(screen.getByRole('textbox')).toBeDisabled();
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });

  test('typing a date after maxDate does not report it and reverts to the prior value on blur', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <DatePicker
        value="2024-03-05"
        onChange={onChange}
        maxDate={new Date(2024, 2, 10)} // Mar 10 2024
      />,
    );

    const input = screen.getByRole('textbox');
    await user.click(input);
    await user.clear(input);
    await typeDigits(user, '03152024'); // Mar 15 — past maxDate

    expect(onChange).not.toHaveBeenCalled();

    await user.tab(); // blur
    expect(input).toHaveValue('03/05/2024');
  });

  test('typing a date before minDate does not report it and reverts on blur', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <DatePicker
        value="2024-03-05"
        onChange={onChange}
        minDate={new Date(2024, 2, 1)} // Mar 1 2024
      />,
    );

    const input = screen.getByRole('textbox');
    await user.click(input);
    await user.clear(input);
    await typeDigits(user, '02152024'); // Feb 15 — before minDate

    expect(onChange).not.toHaveBeenCalled();

    await user.tab();
    expect(input).toHaveValue('03/05/2024');
  });

  test('disableFutureDates disables days after today in the calendar', async () => {
    const user = userEvent.setup();
    render(<ControlledDatePicker disableFutureDates />);

    await user.click(screen.getByRole('textbox'));
    await screen.findByRole('grid');

    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(today.getDate() + 1);

    // Only assert when tomorrow falls in the same displayed month, to keep
    // the test independent of "today".
    if (tomorrow.getMonth() === today.getMonth()) {
      const cell = screen.getByRole('gridcell', { name: String(tomorrow.getDate()) });
      expect(cell.querySelector('button')).toBeDisabled();
    }
  });

  test('typing an out-of-range day (e.g. Jun 31) rolls over to the next valid date, like native Date math — by design', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DatePicker value={undefined} onChange={onChange} />);

    const input = screen.getByRole('textbox');
    await user.click(input);
    await typeDigits(user, '06312024'); // June has 30 days

    // Rolls forward to Jul 1, 2024 — same as `new Date(2024, 5, 31)`.
    expect(onChange).toHaveBeenLastCalledWith('2024-07-01');

    // On blur, the visible text catches up to reflect the resolved date
    // rather than continuing to show the impossible "06/31/2024".
    await user.tab();
    expect(input).toHaveValue('07/01/2024');
  });

  test('blurring with an incomplete typed date reverts to the last selected value', async () => {
    const user = userEvent.setup();
    render(<DatePicker value="2024-03-05" onChange={() => {}} />);

    const input = screen.getByRole('textbox');
    await user.click(input);
    await user.clear(input);
    await typeDigits(user, '0305'); // incomplete

    await user.tab();
    expect(input).toHaveValue('03/05/2024');
  });

  test('blurring with an incomplete typed date and no prior value reverts to empty', async () => {
    const user = userEvent.setup();
    render(<DatePicker value={undefined} onChange={() => {}} />);

    const input = screen.getByRole('textbox');
    await user.click(input);
    await typeDigits(user, '03');

    await user.tab();
    expect(input).toHaveValue('');
  });

  test('an out-of-range parent value (e.g. cleared externally) resets the displayed text', () => {
    const { rerender } = render(<DatePicker value="2024-03-05" onChange={() => {}} />);
    expect(screen.getByRole('textbox')).toHaveValue('03/05/2024');

    rerender(<DatePicker value={undefined} onChange={() => {}} />);
    expect(screen.getByRole('textbox')).toHaveValue('');
  });

  test('focusing the input opens the calendar popover', async () => {
    const user = userEvent.setup();
    render(<DatePicker value={undefined} onChange={() => {}} />);

    expect(screen.queryByRole('grid')).not.toBeInTheDocument();
    await user.click(screen.getByRole('textbox'));
    expect(await screen.findByRole('grid')).toBeInTheDocument();
  });

  test('clicking the calendar icon focuses the input without needing a direct click', async () => {
    const user = userEvent.setup();
    render(<DatePicker value={undefined} onChange={() => {}} />);

    const iconButton = screen.getAllByRole('button')[0];
    await user.click(iconButton);

    expect(screen.getByRole('textbox')).toHaveFocus();
  });

  test('the calendar stays open while typing, including after a full valid date is typed', async () => {
    const user = userEvent.setup();
    render(<ControlledDatePicker />);

    await user.click(screen.getByRole('textbox'));
    expect(await screen.findByRole('grid')).toBeInTheDocument();

    // Type one digit at a time and assert the popover survives each
    // keystroke's re-render, not just the end state.
    for (const digit of '03052024') {
      await user.keyboard(digit);
      expect(screen.getByRole('grid')).toBeInTheDocument();
    }

    // Completing a full valid date fires onChange and updates the
    // calendar, but must not close the popover the way picking a day does.
    expect(screen.getByRole('grid')).toBeInTheDocument();
  });
});
