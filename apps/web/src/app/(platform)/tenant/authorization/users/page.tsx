import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi, ServerApiError } from '@/lib/tenant-api';
import { AuthUserTable } from './AuthUserTable';
import type { TenantUser } from '@/types/tenant';

export default async function AuthorizationUsersPage() {
  const session = await requireTenantAdmin();

  let users: TenantUser[] = [];
  let fetchError: string | null = null;

  try {
    users = await tenantServerApi.getUsers();
  } catch (err) {
    fetchError =
      err instanceof ServerApiError
        ? `Failed to load users (${err.status}).`
        : 'Failed to load users. Is the identity service running?';
  }

  return (
    <div className="space-y-4">
      {fetchError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-base" />
            {fetchError}
          </div>
        </div>
      )}

      {!fetchError && (
        <AuthUserTable users={users} />
      )}
    </div>
  );
}
