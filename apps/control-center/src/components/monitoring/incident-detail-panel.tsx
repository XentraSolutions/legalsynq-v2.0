'use client';

import { useEffect } from 'react';
import type { SystemAlert, IntegrationStatus, AlertSeverity, MonitoringStatus } from '@/types/control-center';

interface IncidentDetailPanelProps {
  alert:       SystemAlert;
  integration: IntegrationStatus | undefined;
  onClose:     () => void;
}

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
 * IncidentDetailPanel — read-only slide-over panel.
 *
 * Opens from the right side of the viewport when an alert row is selected.
 * Shows: severity, message, affected component + current status, timestamps.
 * No mutation or action flows.
 */
export function IncidentDetailPanel({ alert, integration, onClose }: IncidentDetailPanelProps) {
  const sev    = SEVERITY_COLORS[alert.severity];
  const active = !alert.resolvedAtUtc;

  // Dismiss on Escape key
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

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
            <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border ${active ? 'bg-red-50 text-red-600 border-red-200' : 'bg-green-50 text-green-700 border-green-200'}`}>
              {active ? 'Active' : 'Resolved'}
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
                {alert.resolvedAtUtc ? (
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

        {/* Footer note */}
        <div className="px-6 py-3 border-t border-gray-100 bg-gray-50 shrink-0">
          <p className="text-xs text-gray-400 text-center">Read-only view · Data from Monitoring Service</p>
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
