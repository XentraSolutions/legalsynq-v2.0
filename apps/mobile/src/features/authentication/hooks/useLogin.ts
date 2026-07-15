import { useMutation } from '@tanstack/react-query';

import { AuthenticationService } from '@/shared/services/Authentication';
import type { RememberedTenant } from '@/shared/types/tenant';

export function useLogin() {
  return useMutation({
    mutationFn: ({
      email,
      password,
      tenantCode,
      activeTenant,
    }: {
      email: string;
      password: string;
      tenantCode?: string;
      activeTenant?: RememberedTenant | null;
    }) => AuthenticationService.login({ email, password, tenantCode, activeTenant }),
  });
}
