import { fireEvent, render } from '@testing-library/react-native';

import { BiometricLoginButton } from './index';

describe('BiometricLoginButton', () => {
  it('uses the supported biometric label and handles presses', () => {
    const onPress = jest.fn();
    const { getByTestId, getByText } = render(
      <BiometricLoginButton accountLabel="u***@example.com" label="Face ID" onPress={onPress} />
    );

    expect(getByTestId('biometric-login-icon').props.name).toBe('face-recognition');
    expect(getByText('Continue as u***@example.com')).toBeTruthy();
    fireEvent.press(getByText('Sign in with Face ID'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it('uses the fingerprint icon for Touch ID and fingerprint authentication', () => {
    for (const label of ['Touch ID', 'Fingerprint'] as const) {
      const { getByTestId } = render(
        <BiometricLoginButton label={label} onPress={jest.fn()} />
      );

      expect(getByTestId('biometric-login-icon').props.name).toBe('fingerprint');
    }
  });
});
