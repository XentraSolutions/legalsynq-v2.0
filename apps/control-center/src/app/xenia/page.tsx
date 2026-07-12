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
      <div className="mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Dashboard</h2>
        <p className="text-sm text-gray-500 mt-1">
          Xenia service status, module registry, and platform adapter health.
        </p>
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
