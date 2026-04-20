'use client';

import { useEffect, useState, startTransition } from 'react';
import { useRouter } from 'next/navigation';
import type { SystemAlert, IntegrationStatus, AlertSeverity, MonitoringStatus } from '@/types/control-center';
import { resolveAlertAction } from '@/app/actions/monitoring';

interface IncidentDetailPanelProps {
  alert:       SystemAlert;
  integration: IntegrationStatus | undefined;
  onClose:     () => void;
}

type ResolveState = 'idle' | 'submitting' | 'success' | 'error';

const SEVERITY_COLORS: Record<AlertSeverity, { badge: string; bar: string }> = {
  Critical: { badge: 'bg-red-100 text-red-700 border-red-300',    bar: 'bg-red-500'   },
  Warning:  { badge: 'bg-amber-100 text-amber-700 border-amber-300', bar: 'bg-amber-400' },
  Info:     { badge: 'bg-blue-100 text-blue-700 border-blue-200',  bar: 'bg-blue-400'  },
};

const STATUS_COLORS: Record<MonitoringStatus, string> = {
  Healthy:  'bg-green-100 text-green-700 border-green-300',
  Degraded: 'bg-amber-100 text-amber-700 border-amber-300',
  Down:     'bg-red-100   text-red-700   border-red-300',
};

/**
 * IncidentDetailPanel — slide-over detail + action panel.
 *
 * Opens from the right side of the viewport when an alert row is selected.
 * Shows: severity, message, affected component + current status, timestamps.
 *
 * For active alerts, exposes a Resolve button that calls the Monitoring Service
 * admin endpoint through a secure server action. After a successful resolve,
 * triggers router.refresh() to sync the alerts list with the server state.
 */
export function IncidentDetailPanel({ alert, integration, onClose }: IncidentDetailPanelProps) {
  const router = useRouter();
  const sev    = SEVERITY_COLORS[alert.severity];

  // Server-side resolved state (from last fetch)
  const serverActive = !alert.resolvedAtUtc;

  // Local action state — tracks the in-progress resolve workflow
  const [resolveState, setResolveState] = useState<ResolveState>('idle');
  const [resolveError, setResolveError] = useState<string | null>(null);

  // Derived: treat as resolved if either the server says so OR the action succeeded
  const isResolved = !serverActive || resolveState === 'success';

  // Dismiss on Escape key
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  async function handleResolve() {
    setResolveState('submitting');
    setResolveError(null);

    const result = await resolveAlertAction(alert.id);

    if (result.ok) {
      setResolveState('success');
      // Refresh server components so the alerts list reflects the new state.
      startTransition(() => { router.refresh(); });
    } else {
      setResolveState('error');
      setResolveError(result.error ?? 'Failed to resolve alert — please try again.');
    }
  }

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/20 z-40"
        aria-hidden
        onClick={onClose}
      />

      {/* Panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Incident detail"
        className="fixed inset-y-0 right-0 z-50 w-full max-w-md bg-white shadow-xl flex flex-col"
      >
        {/* Severity accent bar */}
        <div className={`h-1 w-full shrink-0 ${sev.bar}`} />

        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div className="flex items-center gap-3">
            <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border ${sev.badge}`}>
              {alert.severity}
            </span>
            <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border ${isResolved ? 'bg-green-50 text-green-700 border-green-200' : 'bg-red-50 text-red-600 border-red-200'}`}>
              {isResolved ? 'Resolved' : 'Active'}
            </span>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close incident detail"
            className="rounded-md p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors"
          >
            <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth={2}>
              <path d="M2 2l12 12M14 2L2 14" strokeLinecap="round" />
            </svg>
          </button>
        </div>

        {/* Scrollable body */}
        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">

          {/* Alert message */}
          <Section title="Incident Message">
            <p className="text-sm text-gray-800 leading-relaxed">{alert.message}</p>
          </Section>

          {/* Affected component */}
          <Section title="Affected Component">
            {integration ? (
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-900">{integration.name}</span>
                  <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border ${STATUS_COLORS[integration.status]}`}>
                    {integration.status}
                  </span>
                </div>
                {integration.category && (
                  <Row label="Category">
                    <span className="text-xs font-medium uppercase tracking-wide text-gray-500">
                      {integration.category}
                    </span>
                  </Row>
                )}
                <Row label="Latency">
                  <LatencyValue ms={integration.latencyMs} />
                </Row>
                <Row label="Last checked">
                  <span className="text-sm text-gray-700 tabular-nums">
                    {formatTimestamp(integration.lastCheckedAtUtc)}
                  </span>
                </Row>
              </div>
            ) : alert.entityName ? (
              <div className="space-y-2">
                <span className="text-sm font-medium text-gray-900">{alert.entityName}</span>
                <p className="text-xs text-gray-400">Current status unavailable — component not found in integration list.</p>
              </div>
            ) : (
              <p className="text-sm text-gray-400">No component information available.</p>
            )}
          </Section>

          {/* Timestamps */}
          <Section title="Timestamps">
            <div className="space-y-2">
              <Row label="Created">
                <span className="text-sm text-gray-700 tabular-nums">
                  {formatFullTimestamp(alert.createdAtUtc)}
                </span>
              </Row>
              <Row label="Resolved">
                {resolveState === 'success' ? (
                  <span className="text-sm text-green-700 tabular-nums">Operator resolved — refreshing…</span>
                ) : alert.resolvedAtUtc ? (
                  <span className="text-sm text-green-700 tabular-nums">
                    {formatFullTimestamp(alert.resolvedAtUtc)}
                  </span>
                ) : (
                  <span className="text-sm text-red-600 font-medium">Unresolved</span>
                )}
              </Row>
            </div>
          </Section>

          {/* Alert ID (for ops reference) */}
          <Section title="Alert Reference">
            <code className="text-xs text-gray-400 font-mono break-all">{alert.id}</code>
          </Section>

        </div>

        {/* Footer — action area */}
        <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 shrink-0 space-y-3">

          {/* Resolve button — only for active alerts not yet resolved by this session */}
          {!isResolved && (
            <button
              type="button"
              onClick={handleResolve}
              disabled={resolveState === 'submitting'}
              className="w-full inline-flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-medium text-white bg-gray-800 hover:bg-gray-700 disabled:opacity-60 disabled:cursor-not-allowed transition-colors"
            >
              {resolveState === 'submitting' ? (
                <>
                  <svg className="h-3.5 w-3.5 animate-spin" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5}>
                    <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83" strokeLinecap="round" />
                  </svg>
                  Resolving…
                </>
              ) : (
                <>
                  <svg className="h-3.5 w-3.5" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth={2}>
                    <path d="M13.5 4.5l-7 7L2.5 7.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  Resolve Alert
                </>
              )}
            </button>
          )}

          {/* Success state */}
          {resolveState === 'success' && (
            <div className="flex items-center gap-2 text-sm text-green-700">
              <svg className="h-4 w-4 shrink-0" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth={2}>
                <path d="M13.5 4.5l-7 7L2.5 7.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              Alert resolved — data refreshing.
            </div>
          )}

          {/* Error state */}
          {resolveState === 'error' && resolveError && (
            <div className="rounded-md bg-red-50 border border-red-200 px-3 py-2">
              <p className="text-xs text-red-700 leading-relaxed">{resolveError}</p>
              <button
                type="button"
                onClick={() => { setResolveState('idle'); setResolveError(null); }}
                className="mt-1.5 text-xs text-red-600 underline hover:text-red-800"
              >
                Dismiss
              </button>
            </div>
          )}

          <p className="text-xs text-gray-400 text-center">
            {isResolved ? 'Resolved · Data from Monitoring Service' : 'Actions execute through Monitoring Service · Data is authoritative'}
          </p>
        </div>
      </div>
    </>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="text-[11px] font-semibold uppercase tracking-wider text-gray-400 mb-2">
        {title}
      </h3>
      {children}
    </div>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4">
      <span className="text-xs text-gray-400 w-24 shrink-0 pt-0.5">{label}</span>
      <div className="flex-1 text-right">{children}</div>
    </div>
  );
}

function LatencyValue({ ms }: { ms?: number }) {
  if (ms === undefined) return <span className="text-sm text-gray-400">—</span>;
  const color =
    ms > 1000 ? 'text-red-600 font-semibold' :
    ms > 400  ? 'text-amber-600 font-semibold' :
                'text-gray-700';
  return <span className={`text-sm tabular-nums ${color}`}>{ms} ms</span>;
}

// ── helpers ────────────────────────────────────────────────────────────────────

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour: '2-digit', minute: '2-digit', second: '2-digit',
      hour12: false, timeZone: 'UTC', timeZoneName: 'short',
    });
  } catch { return iso; }
}

function formatFullTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      year: 'numeric', month: 'short', day: 'numeric',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
      hour12: false, timeZone: 'UTC', timeZoneName: 'short',
    });
  } catch { return iso; }
}
