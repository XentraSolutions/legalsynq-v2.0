import type { UserSession } from '@/shared/types/auth';

import type { LegacyLoginResponse } from './legacyEndpoints';

function splitPermissions(permission: string | undefined): string[] {
  if (!permission) return [];
  return permission
    .split(',')
    .map((entry) => entry.trim())
    .filter(Boolean);
}

export const LegacyAuthenticationAdapter = {
  toUserSession(response: LegacyLoginResponse): UserSession {
    const data = response.data ?? {};
    const email = data.email ?? '';
    const firstName = data.firstName ?? '';
    const lastName = data.lastName ?? '';
    const programId = data.programId != null ? String(data.programId) : '';

    return {
      id: email || `${firstName}${lastName}` || 'legacy-user',
      email,
      firstName,
      lastName,
      roles: data.userType ? [data.userType] : [],
      permissions: splitPermissions(data.permission),
      organization: {
        id: programId,
        name: 'SynqLiens',
        tenantId: programId,
      },
      tenantId: programId,
    };
  },
};
