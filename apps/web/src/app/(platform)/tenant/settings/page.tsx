import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi, ServerApiError } from '@/lib/tenant-api';
import { TenantMapProviderCard } from './TenantMapProviderCard';

export const dynamic = 'force-dynamic';


export default async function TenantSettingsPage() {
  const session = await requireTenantAdmin();

  let currentProvider: 'google' | 'osm' = 'google';
  let fetchError: string | null = null;

  try {
    const setting = await tenantServerApi.getMapProviderSetting(session.tenantId);
    if (setting.value === 'osm' || setting.value === 'google') {
      currentProvider = setting.value;
    }
  } catch (err) {
    fetchError = err instanceof ServerApiError && err.isForbidden
      ? 'You do not have permission to view map settings.'
      : null;
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-2xl mx-auto px-4 py-10 space-y-6">

        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Tenant Settings</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Configure tenant-wide settings that apply to all users.
            </p>
          </div>
          <span className="text-xs bg-gray-100 border border-gray-200 text-gray-500 px-2 py-1 rounded">
            {session.isPlatformAdmin ? 'Platform Admin' : 'Tenant Admin'}
          </span>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            <div className="flex items-center gap-2">
              <i className="ri-error-warning-line text-base" />
              {fetchError}
            </div>
          </div>
        )}

        <TenantMapProviderCard
          tenantId={session.tenantId}
          initialProvider={currentProvider}
        />

      </div>
    </div>
  );
}
