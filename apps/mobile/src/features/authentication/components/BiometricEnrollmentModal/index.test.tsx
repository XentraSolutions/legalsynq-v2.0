import { fireEvent, render } from '@testing-library/react-native';
import { Provider, createStore } from 'jotai';

import { biometricEnrollmentOfferAtom } from '@/shared/state/atoms/biometricAtom';

import { BiometricEnrollmentModal } from './index';

describe('BiometricEnrollmentModal', () => {
  it('allows the user to postpone enrollment', () => {
    const store = createStore();
    store.set(biometricEnrollmentOfferAtom, { label: 'Face ID', visible: true });
    const { getByText, queryByText } = render(
      <Provider store={store}>
        <BiometricEnrollmentModal />
      </Provider>
    );

    expect(getByText('Enable Biometric Login')).toBeTruthy();
    expect(getByText(/Use Face ID/)).toBeTruthy();

    fireEvent.press(getByText('Not Now'));

    expect(queryByText('Enable Biometric Login')).toBeNull();
  });
});
