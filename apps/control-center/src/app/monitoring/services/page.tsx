import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { listServices } from '@/lib/system-health-store';
import { listAudit } from '@/lib/system-health-audit';
import { ServicesEditor } from '@/components/monitoring/services-editor';
import { ServicesAuditList } from '@/components/monitoring/services-audit-list';
import Link from 'next/link';

export const dynamic = 'force-dynamic';

export default async function MonitoringServicesPage() {
  const session = await requirePlatformAdmin();
  const [services, auditEntries] = await Promise.all([
    listServices(),
    listAudit(20),
  ]);

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

          <div className="mt-8">
            <div className="flex items-center justify-between mb-2">
              <p className="text-xs text-gray-500">
                Service-config changes are also forwarded to the central audit log.
              </p>
              <Link
                href="/audit-logs?eventType=monitoring.service.changed"
                className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800"
              >
                View in Audit Logs <span aria-hidden>→</span>
              </Link>
            </div>
            <ServicesAuditList entries={auditEntries} />
          </div>

        </div>
      </div>
    </CCShell>
  );
}
