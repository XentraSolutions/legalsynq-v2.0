import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getXeniaAdapters, type XeniaAdapterDto } from '@/lib/xenia-api';
import { XeniaAdaptersTable } from '@/components/xenia/xenia-adapters-table';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

export const dynamic = 'force-dynamic';

export default async function XeniaAdaptersPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let adapters: XeniaAdapterDto[] = [];
  let error = false;

  try {
    adapters = await getXeniaAdapters(token);
  } catch {
    error = true;
  }

  const unconfiguredCount = adapters.filter((a) => a.configuration_status === 'Unconfigured').length;

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Platform Adapters</h2>
        <p className="text-sm text-gray-500 mt-1">
          Platform adapter interfaces through which Xenia accesses external services.
          Adapters report their configuration and health status honestly.
        </p>
      </div>

      {error ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service. Ensure it is running on port 5035.
        </div>
      ) : (
        <>
          {unconfiguredCount > 0 && (
            <div className="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-700">
              <strong>{unconfiguredCount} adapter{unconfiguredCount !== 1 ? 's' : ''} unconfigured.</strong>{' '}
              This is expected for the platform foundation. Real adapters will be wired in subsequent implementation tickets.
              All unconfigured adapters return explicit unavailable status — they never report false success.
            </div>
          )}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100">
              <p className="text-sm font-medium text-gray-700">
                {adapters.length} adapter{adapters.length !== 1 ? 's' : ''} registered
              </p>
            </div>
            <XeniaAdaptersTable adapters={adapters} />
          </div>
        </>
      )}
    </div>
  );
}
