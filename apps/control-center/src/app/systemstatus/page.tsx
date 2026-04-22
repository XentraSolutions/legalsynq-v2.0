import { SystemStatusAutoRefresh } from '@/components/monitoring/system-status-auto-refresh';
import type { MonitoringSummary }  from '@/types/control-center';

export const dynamic = 'force-dynamic';

async function fetchMonitoringSummary(): Promise<MonitoringSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const res = await fetch(`${base}/api/monitoring/summary`, { cache: 'no-store' });
  if (!res.ok) throw new Error(`Health probe failed: ${res.status}`);
  return res.json();
}

export default async function SystemStatusPage() {
  let data:       MonitoringSummary | null = null;
  let fetchError: string | null           = null;

  try {
    data = await fetchMonitoringSummary();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load monitoring data.';
  }

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-4xl mx-auto px-6 py-3 flex items-center justify-between gap-4">
          <a href="/" className="text-sm font-semibold text-gray-900 hover:text-indigo-700">
            LegalSynq
          </a>
          <nav className="flex items-center gap-4 text-sm">
            <a href="/systemstatus" className="text-gray-600 hover:text-gray-900">
              Status
            </a>
            <a
              href="/login"
              className="inline-flex items-center px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700"
            >
              Sign in
            </a>
          </nav>
        </div>
      </header>

      <div className="max-w-4xl mx-auto px-6 py-8 w-full flex-1">
        <SystemStatusAutoRefresh initialData={data} initialError={fetchError} />
      </div>

      <footer className="border-t border-gray-200 bg-white">
        <div className="max-w-4xl mx-auto px-6 py-4 flex flex-col sm:flex-row items-center justify-between gap-2 text-xs text-gray-500">
          <span>&copy; {new Date().getFullYear()} LegalSynq</span>
          <nav className="flex items-center gap-4">
            <a href="/login" className="hover:text-gray-900">Sign in</a>
            <a href="/systemstatus" className="hover:text-gray-900">Status</a>
          </nav>
        </div>
      </footer>
    </div>
  );
}
