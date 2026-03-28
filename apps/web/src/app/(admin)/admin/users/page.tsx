import { requireAdmin } from '@/lib/auth-guards';

/**
 * Admin — User management.
 * TenantAdmin sees users within their tenant.
 * PlatformAdmin sees all users (across tenants — not yet scoped here).
 */
export default async function AdminUsersPage() {
  const session = await requireAdmin();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Users</h1>
        <span className="text-xs bg-gray-100 border border-gray-200 text-gray-500 px-2 py-1 rounded">
          {session.isPlatformAdmin ? 'Platform Admin' : 'Tenant Admin'}
        </span>
      </div>

      {/* TODO: Replace with UserTable component fetching from apiClient */}
      <div className="bg-white border border-gray-200 rounded-lg p-8 text-center text-sm text-gray-400">
        User list — connect to{' '}
        <code className="font-mono bg-gray-100 px-1 rounded">
          GET /api/identity/api/users
        </code>
      </div>
    </div>
  );
}
