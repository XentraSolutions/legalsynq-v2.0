import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import {
  getXeniaInfo,
  getXeniaReady,
  getXeniaModules,
  getXeniaAdapters,
} from '@/lib/xenia-api';
import { XeniaDashboard } from '@/components/xenia/xenia-dashboard';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

export const dynamic = 'force-dynamic';

export default async function XeniaDashboardPage() {
  await requirePlatformAdmin();

  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  const [info, ready, modules, adapters] = await Promise.all([
    getXeniaInfo(),
    getXeniaReady(),
    getXeniaModules(token),
    getXeniaAdapters(token),
  ]);

  const isServiceReachable = info !== null;

  return (
    <div>
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Dashboard</h2>
          <p className="mt-1 text-sm text-gray-500">
            Xenia service status, module registry, platform adapter health, and assistant runtime entry points.
          </p>
        </div>
        <a
          href="/xenia/settings"
          className="inline-flex items-center gap-2 rounded-md border border-indigo-200 bg-indigo-50 px-4 py-2 text-sm font-medium text-indigo-700 hover:bg-indigo-100"
        >
          <i className="ri-settings-3-line text-base" aria-hidden />
          Xenia Assistant Settings
        </a>
      </div>

      <XeniaDashboard
        info={info}
        ready={ready}
        modules={modules}
        adapters={adapters}
        isServiceReachable={isServiceReachable}
      />
    </div>
  );
}
