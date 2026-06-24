import { useMutation } from '@tanstack/react-query';

import { AuthenticationService } from '@/shared/services/Authentication';

export function useLogin() {
  return useMutation({
    mutationFn: ({
      email,
      password,
      tenantCode,
    }: {
      email: string;
      password: string;
      tenantCode?: string;
    }) => AuthenticationService.login(email, password, tenantCode),
  });
}
