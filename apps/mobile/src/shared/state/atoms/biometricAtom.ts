import { atom } from 'jotai';

import type { BiometricLabel } from '@/shared/services/DeviceSecurity';

export interface BiometricEnrollmentOfferState {
  label: BiometricLabel;
  visible: boolean;
}

export const biometricEnrollmentOfferAtom = atom<BiometricEnrollmentOfferState>({
  label: 'Biometrics',
  visible: false,
});
