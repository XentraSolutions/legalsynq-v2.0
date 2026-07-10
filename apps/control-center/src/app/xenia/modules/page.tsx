import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getXeniaModules, type XeniaModuleDto } from '@/lib/xenia-api';
import { XeniaModulesTable } from '@/components/xenia/xenia-modules-table';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

export const dynamic = 'force-dynamic';

export default async function XeniaModulesPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let modules: XeniaModuleDto[] = [];
  let error = false;

  try {
    modules = await getXeniaModules(token);
  } catch {
    error = true;
  }

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Modules</h2>
        <p className="text-sm text-gray-500 mt-1">
          Registered Xenia automation modules. Modules encapsulate specific automation capabilities.
        </p>
      </div>

      {error ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service. Ensure it is running on port 5035.
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
            <p className="text-sm font-medium text-gray-700">
              {modules.length} module{modules.length !== 1 ? 's' : ''} registered
            </p>
            <span className="text-xs text-gray-400">
              Email module will appear after XENIA-P1-T2
            </span>
          </div>
          <XeniaModulesTable modules={modules} />
        </div>
      )}
    </div>
  );
}
