import { fireEvent, render } from '@testing-library/react-native';

import { BiometricLoginButton } from './index';

describe('BiometricLoginButton', () => {
  it('uses the supported biometric label and handles presses', () => {
    const onPress = jest.fn();
    const { getByText } = render(
      <BiometricLoginButton accountLabel="u***@example.com" label="Face ID" onPress={onPress} />
    );

    expect(getByText('Continue as u***@example.com')).toBeTruthy();
    fireEvent.press(getByText('Sign in with Face ID'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
