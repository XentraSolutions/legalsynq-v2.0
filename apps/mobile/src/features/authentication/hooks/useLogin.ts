import { useMutation } from '@tanstack/react-query';
import { useSetAtom } from 'jotai';

import {
  AuthenticationService,
  BiometricAuthenticationService,
} from '@/shared/services/Authentication';
import { biometricEnrollmentOfferAtom } from '@/shared/state/atoms/biometricAtom';
import type { RememberedTenant } from '@/shared/types/tenant';

export function useLogin() {
  const setEnrollmentOffer = useSetAtom(biometricEnrollmentOfferAtom);

  return useMutation({
    mutationFn: async ({
      email,
      password,
      tenantCode,
      activeTenant,
    }: {
      email: string;
      password: string;
      tenantCode?: string;
      activeTenant?: RememberedTenant | null;
    }) => {
      const outcome = await AuthenticationService.login({
        email,
        password,
        tenantCode,
        activeTenant,
      });

      try {
        const offer = await BiometricAuthenticationService.prepareEnrollment(
          outcome.biometricEnrollment
        );
        setEnrollmentOffer({
          label: offer.label,
          visible: offer.shouldOffer,
        });
      } catch {
        setEnrollmentOffer({ label: 'Biometrics', visible: false });
      }

      return outcome.user;
    },
  });
}
