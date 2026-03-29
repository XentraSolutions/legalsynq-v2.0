import type { AuthPrincipal } from '@/domain/interfaces/auth-provider';
import { Role } from '@/shared/constants';
import { ForbiddenError } from '@/shared/errors';

type Action = 'read' | 'write' | 'delete' | 'admin';

/**
 * RBAC + ABAC enforcement.
 *
 * Default-DENY model: permission must be explicitly granted.
 * Tenant isolation: tenantId on the principal is always matched against the resource.
 */
const ROLE_PERMISSIONS: Record<string, Action[]> = {
  [Role.PLATFORM_ADMIN]: ['read', 'write', 'delete', 'admin'],
  [Role.TENANT_ADMIN]:   ['read', 'write', 'delete'],
  [Role.DOC_MANAGER]:    ['read', 'write', 'delete'],
  [Role.DOC_UPLOADER]:   ['read', 'write'],
  [Role.DOC_READER]:     ['read'],
};

export function assertPermission(principal: AuthPrincipal, action: Action): void {
  const allowed = principal.roles.some(
    (role) => ROLE_PERMISSIONS[role]?.includes(action),
  );

  if (!allowed) {
    throw new ForbiddenError(
      `Role(s) [${principal.roles.join(', ')}] do not have '${action}' permission`,
    );
  }
}

/** Assert that the principal belongs to the tenant they are operating on */
export function assertTenantScope(principal: AuthPrincipal, resourceTenantId: string): void {
  if (
    principal.tenantId !== resourceTenantId &&
    !principal.roles.includes(Role.PLATFORM_ADMIN)
  ) {
    throw new ForbiddenError('Cross-tenant access denied');
  }
}
