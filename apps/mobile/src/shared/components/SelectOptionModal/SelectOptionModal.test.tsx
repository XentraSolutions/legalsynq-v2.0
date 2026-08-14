import { fireEvent, render } from '@testing-library/react-native';

import { SelectOptionModal } from './SelectOptionModal';

const options = Array.from({ length: 21 }, (_, index) => ({
  label: `Option ${index + 1}`,
  value: `option-${index + 1}`,
}));

describe('SelectOptionModal', () => {
  it('pins the selected option first and enables search for long lists', () => {
    const { getAllByTestId, getByPlaceholderText } = render(
      <SelectOptionModal
        options={options}
        selectedValue="option-21"
        title="State"
        visible
        onClose={jest.fn()}
        onSelect={jest.fn()}
      />
    );

    expect(getAllByTestId(/^select-option-/)[0].props.accessibilityState).toEqual({
      selected: true,
    });
    expect(getByPlaceholderText('Search state...')).toBeTruthy();
  });

  it('filters options by label or value', () => {
    const { getByLabelText, getByPlaceholderText, getByText, queryByText } = render(
      <SelectOptionModal
        options={options}
        title="State"
        visible
        onClose={jest.fn()}
        onSelect={jest.fn()}
      />
    );

    fireEvent.changeText(getByPlaceholderText('Search state...'), 'Option 20');

    expect(getByText('Option 20')).toBeTruthy();
    expect(queryByText('Option 1')).toBeNull();

    fireEvent.press(getByLabelText('Clear search'));

    expect(getByPlaceholderText('Search state...').props.value).toBe('');
    expect(getByText('Option 1')).toBeTruthy();
  });
});
