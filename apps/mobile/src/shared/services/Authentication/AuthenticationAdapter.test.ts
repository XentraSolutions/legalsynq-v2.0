import type { SessionEnvelope } from '@/shared/types/auth';

import { AuthenticationAdapter } from './AuthenticationAdapter';

describe('AuthenticationAdapter', () => {
  it('maps an access token and session envelope into auth state', () => {
    const sessionEnvelope: SessionEnvelope = {
      issuedAt: '2026-06-24T00:00:00Z',
      expiresAt: '2026-06-24T08:00:00Z',
      tenantId: 'tenant-demo',
      user: {
        id: 'usr-1',
        email: 'demo@legalsynq.com',
        firstName: 'Avery',
        lastName: 'Mendoza',
        roles: ['TenantAdmin'],
        permissions: ['liens.read'],
        organization: {
          id: 'org-1',
          name: 'Smith Law Firm',
          tenantId: 'tenant-demo',
        },
        tenantId: 'tenant-demo',
      },
    };

    expect(AuthenticationAdapter.toAuthState('token', sessionEnvelope)).toEqual({
      user: sessionEnvelope.user,
      token: 'token',
      isAuthenticated: true,
    });
  });
});
