import { AuthenticationAdapter } from './AuthenticationAdapter';

describe('AuthenticationAdapter', () => {
  it('maps the backend login response into auth state', () => {
    const user = AuthenticationAdapter.toUserSession({
      accessToken: 'token',
      expiresAtUtc: '2026-06-24T08:00:00Z',
      user: {
        id: 'usr-1',
        tenantId: 'tenant-1',
        email: 'avery.mendoza@smithlaw.example',
        firstName: 'Avery',
        lastName: 'Mendoza',
        isActive: true,
        roles: ['TenantAdmin'],
        organizationId: 'org-1',
        orgType: 'Smith Law Firm',
        productRoles: ['SYNQLIEN_SELLER'],
      },
      tenants: [{ tenantId: 'tenant-1', tenantCode: 'smith-law' }],
    });

    expect(AuthenticationAdapter.toAuthState('token', user)).toEqual({
      user: {
        id: 'usr-1',
        email: 'avery.mendoza@smithlaw.example',
        firstName: 'Avery',
        lastName: 'Mendoza',
        roles: ['TenantAdmin', 'SYNQLIEN_SELLER'],
        permissions: [],
        organization: {
          id: 'org-1',
          name: 'Smith Law Firm',
          tenantId: 'tenant-1',
        },
        tenantId: 'tenant-1',
      },
      token: 'token',
      isAuthenticated: true,
    });
  });
});
