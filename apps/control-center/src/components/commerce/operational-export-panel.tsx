'use client';

import { useState } from 'react';

export function OperationalExportPanel() {
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [lastExported, setLastExported] = useState<string | null>(null);

  async function downloadExport() {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/commerce/export/operational-summary', {
        credentials: 'include',
      });

      if (!res.ok) {
        let msg = `Export failed (HTTP ${res.status}).`;
        try {
          const body = await res.json() as Record<string, unknown>;
          if (body.error) msg = String(body.error);
        } catch { /* ignore */ }
        setError(msg);
        return;
      }

      const disposition = res.headers.get('Content-Disposition') ?? '';
      const match       = disposition.match(/filename="([^"]+)"/);
      const filename    = match ? match[1] : 'legalsynq-ops-export.json';

      const blob = await res.blob();
      const url  = URL.createObjectURL(blob);
      const a    = document.createElement('a');
      a.href     = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      setLastExported(new Date().toLocaleString());
    } catch {
      setError('Export request failed — unable to reach server.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 bg-slate-50 border-b border-slate-200 flex items-center gap-2">
        <i className="ri-download-cloud-line text-indigo-500" />
        <h2 className="text-sm font-semibold text-slate-700">Operational Export</h2>
        <span className="ml-auto inline-flex items-center text-[10px] font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-500">
          JSON · Read-only
        </span>
      </div>

      <div className="p-5">
        <p className="text-xs text-slate-600 mb-4">
          Downloads a read-only operational summary as JSON — includes Commerce bridge diagnostics,
          admin dashboard summary, and Billing service health.
          No payment data, secrets, or connection strings are included.
        </p>

        <div className="flex items-center gap-3">
          <button
            onClick={downloadExport}
            disabled={loading}
            className="flex items-center gap-2 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            <i className={loading ? 'ri-loader-4-line animate-spin' : 'ri-download-line'} />
            {loading ? 'Preparing export…' : 'Download JSON Export'}
          </button>

          {lastExported && (
            <span className="text-xs text-slate-500">
              Last exported: {lastExported}
            </span>
          )}
        </div>

        {error && (
          <div className="mt-3 flex items-start gap-2 bg-red-50 border border-red-200 rounded-md px-4 py-3 text-sm text-red-700">
            <i className="ri-error-warning-line mt-0.5 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <div className="mt-4 text-xs text-slate-400 flex items-start gap-1.5">
          <i className="ri-lock-line mt-0.5 shrink-0" />
          <span>PlatformAdmin only. Export is generated server-side and contains no sensitive credentials.</span>
        </div>
      </div>
    </section>
  );
}
