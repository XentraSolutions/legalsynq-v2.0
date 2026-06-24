import { fireEvent, render } from '@testing-library/react-native';

import { Button } from './Button';

describe('Button', () => {
  it('renders the label and handles presses', async () => {
    const onPress = jest.fn();
    const { getByText } = await render(<Button label="Sign In" onPress={onPress} />);

    fireEvent.press(getByText('Sign In'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it('hides the label while loading', async () => {
    const { queryByText } = await render(<Button label="Submit" loading />);

    expect(queryByText('Submit')).toBeNull();
  });
});
