import type { LoginResponse } from '@/shared/api/endpoints/Authentication';
import type { AuthState, UserSession } from '@/shared/types/auth';

export const AuthenticationAdapter = {
  toUserSession(response: LoginResponse): UserSession {
    const { user } = response;
    const tenant =
      response.tenants?.find((item) => item.tenantId === user.tenantId) ?? response.tenants?.[0];

    return {
      id: user.id,
      email: user.email,
      firstName: user.firstName,
      lastName: user.lastName,
      roles: Array.from(new Set([...user.roles, ...(user.productRoles ?? [])])),
      permissions: [],
      organization: {
        id: user.organizationId ?? user.tenantId,
        name: user.orgType ?? tenant?.tenantCode ?? 'LegalSynq',
        tenantId: user.tenantId,
      },
      tenantId: user.tenantId,
    };
  },

  toAuthState(accessToken: string, user: UserSession): AuthState {
    return {
      user,
      token: accessToken,
      isAuthenticated: true,
      status: 'authenticated',
      sessionVersion: 0,
    };
  },
};
