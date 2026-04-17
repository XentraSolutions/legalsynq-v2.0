import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { listServices } from '@/lib/system-health-store';
import { ServicesEditor } from '@/components/monitoring/services-editor';
import Link from 'next/link';

export const dynamic = 'force-dynamic';

export default async function MonitoringServicesPage() {
  const session = await requirePlatformAdmin();
  const services = await listServices();

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-4xl mx-auto px-6 py-8">

          <div className="mb-6">
            <Link
              href="/monitoring"
              className="text-xs text-gray-500 hover:text-gray-700 inline-flex items-center gap-1"
            >
              <span>←</span> Back to System Health
            </Link>
            <h1 className="text-xl font-semibold text-gray-900 mt-2">Probed Services</h1>
            <p className="text-sm text-gray-500 mt-1">
              Add, rename, or remove services that the System Health monitor probes.
              Changes take effect on the next refresh — no redeploy required.
            </p>
          </div>

          <ServicesEditor initialServices={services} />

        </div>
      </div>
    </CCShell>
  );
}
