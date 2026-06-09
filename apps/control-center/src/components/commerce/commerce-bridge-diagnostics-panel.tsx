'use client';

import type { CommerceBridgeDiagnostics } from '@/types/control-center';

interface Props {
  diagnostics: CommerceBridgeDiagnostics | null;
  error?:      string | null;
}

function cbStateColor(state: string): string {
  const s = state.toLowerCase();
  if (s === 'closed')   return 'bg-emerald-100 text-emerald-800';
  if (s === 'open')     return 'bg-red-100    text-red-800';
  if (s === 'halfopen') return 'bg-amber-100  text-amber-800';
  return 'bg-slate-100 text-slate-500';
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-4 py-1.5 border-b border-slate-100 last:border-0 text-sm">
      <span className="text-slate-500 shrink-0">{label}</span>
      <span className="text-slate-800 font-medium text-right">{children}</span>
    </div>
  );
}

function BoolFlag({ value, trueLabel = 'Enabled', falseLabel = 'Disabled' }: {
  value: boolean;
  trueLabel?: string;
  falseLabel?: string;
}) {
  return value
    ? <span className="text-emerald-600 font-semibold">{trueLabel}</span>
    : <span className="text-slate-400">{falseLabel}</span>;
}

function fmt(iso: string | null | undefined): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

export function CommerceBridgeDiagnosticsPanel({ diagnostics, error }: Props) {
  return (
    <section className="bg-white border border-slate-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 bg-slate-50 border-b border-slate-200 flex items-center gap-2">
        <i className="ri-link-m text-indigo-500" />
        <h2 className="text-sm font-semibold text-slate-700">
          Commerce → Tenant Billing Bridge
        </h2>
        {diagnostics && (
          <span className={`ml-auto inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-semibold ${
            diagnostics.enabled ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-500'
          }`}>
            {diagnostics.enabled ? 'Bridge Enabled' : 'Bridge Disabled'}
          </span>
        )}
      </div>

      <div className="p-5">
        {error && (
          <div className="flex items-start gap-2 rounded-md bg-amber-50 border border-amber-200 px-4 py-3 text-sm text-amber-800 mb-4">
            <i className="ri-error-warning-line mt-0.5 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {!diagnostics && !error && (
          <div className="flex items-center gap-2 text-sm text-slate-400 py-4">
            <i className="ri-information-line" />
            Bridge diagnostics unavailable.
          </div>
        )}

        {diagnostics && !diagnostics.enabled && !error && (
          <div className="flex items-start gap-2 rounded-md bg-slate-50 border border-slate-200 px-4 py-3 text-sm text-slate-600 mb-4">
            <i className="ri-information-line mt-0.5 shrink-0" />
            The Commerce → Tenant Billing bridge is currently disabled. Entitlement snapshots are not being published automatically.
          </div>
        )}

        {diagnostics && (
          <div className="space-y-0.5">
            <Row label="Bridge Enabled">
              <BoolFlag value={diagnostics.enabled} />
            </Row>
            <Row label="Base URL Configured">
              <BoolFlag value={diagnostics.baseUrlConfigured} trueLabel="Yes" falseLabel="No" />
            </Row>
            <Row label="Internal Token Configured">
              <BoolFlag value={diagnostics.internalTokenConfigured} trueLabel="Yes" falseLabel="No" />
            </Row>
            <Row label="Mode">{diagnostics.mode}</Row>
            <Row label="Target Route">
              <span className="font-mono text-xs">{diagnostics.targetRoute || '—'}</span>
            </Row>
            <Row label="Timeout">{diagnostics.timeoutSeconds}s</Row>
            <Row label="Retry Attempts">{diagnostics.retryAttempts}</Row>
            <Row label="Circuit Breaker">
              {diagnostics.circuitBreakerEnabled ? (
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold ${cbStateColor(diagnostics.circuitBreakerState)}`}>
                  {diagnostics.circuitBreakerState}
                </span>
              ) : <span className="text-slate-400">Disabled</span>}
            </Row>
            {diagnostics.autoPublishEnabled && (
              <Row label="Auto-Publish Queue">{diagnostics.autoPublishQueueDepth} queued</Row>
            )}
            {diagnostics.outboxEnabled && (
              <>
                <Row label="Outbox Enabled">
                  <BoolFlag value={diagnostics.outboxEnabled} />
                </Row>
                <Row label="Outbox Pending">{diagnostics.outboxPendingCount}</Row>
                <Row label="Outbox Failed">{diagnostics.outboxFailedCount}</Row>
                <Row label="Outbox Published">{diagnostics.outboxPublishedCount}</Row>
              </>
            )}
            <Row label="Last Checked">{fmt(diagnostics.lastCheckedAtUtc)}</Row>
          </div>
        )}
      </div>
    </section>
  );
}
