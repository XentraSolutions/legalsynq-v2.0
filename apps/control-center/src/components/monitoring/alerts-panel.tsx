'use client';

import { useState } from 'react';
import type { SystemAlert, AlertSeverity, IntegrationStatus } from '@/types/control-center';
import { IncidentDetailPanel } from './incident-detail-panel';

interface AlertsPanelProps {
  alerts:        SystemAlert[];
  integrations?: IntegrationStatus[]; // optional; correlation works when provided
}

const SEVERITY_CONFIG: Record<AlertSeverity, {
  bg:     string;
  border: string;
  icon:   string;
  label:  string;
  badge:  string;
}> = {
  Critical: {
    bg:     'bg-red-50',
    border: 'border-red-200',
    icon:   'text-red-600',
    label:  'Critical',
    badge:  'bg-red-100 text-red-700 border-red-300',
  },
  Warning: {
    bg:     'bg-amber-50',
    border: 'border-amber-200',
    icon:   'text-amber-600',
    label:  'Warning',
    badge:  'bg-amber-100 text-amber-700 border-amber-300',
  },
  Info: {
    bg:     'bg-blue-50',
    border: 'border-blue-100',
    icon:   'text-blue-500',
    label:  'Info',
    badge:  'bg-blue-100 text-blue-700 border-blue-200',
  },
};

const SEVERITY_ICONS: Record<AlertSeverity, string> = {
  Critical: '🔴',
  Warning:  '⚠️',
  Info:     'ℹ️',
};

const SEVERITY_ORDER: Record<AlertSeverity, number> = {
  Critical: 0,
  Warning:  1,
  Info:     2,
};

/**
 * AlertsPanel — active system alerts with click-to-open incident detail.
 *
 * Client component: manages selected alert state locally.
 * Opens IncidentDetailPanel (slide-over) when a row is clicked.
 * Correlates alert → integration by entityName ↔ name (name-based correlation).
 * No mutation flows; read-only.
 */
export function AlertsPanel({ alerts, integrations = [] }: AlertsPanelProps) {
  const [selectedAlert, setSelectedAlert] = useState<SystemAlert | null>(null);

  const sorted = [...alerts].sort((a, b) => {
    const diff = SEVERITY_ORDER[a.severity] - SEVERITY_ORDER[b.severity];
    return diff !== 0 ? diff : b.createdAtUtc.localeCompare(a.createdAtUtc);
  });

  const criticalCount = alerts.filter(a => a.severity === 'Critical').length;
  const warningCount  = alerts.filter(a => a.severity === 'Warning').length;

  // Name-based correlation: alert.entityName → integrations[].name
  const relatedIntegration: IntegrationStatus | undefined = selectedAlert?.entityName
    ? integrations.find(i => i.name === selectedAlert.entityName)
    : undefined;

  return (
    <>
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

        {/* Header */}
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            System Alerts
          </h2>
          <div className="flex items-center gap-2">
            {criticalCount > 0 && (
              <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-red-100 text-red-700 border border-red-300">
                {criticalCount} Critical
              </span>
            )}
            {warningCount > 0 && (
              <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 border border-amber-300">
                {warningCount} Warning
              </span>
            )}
            <span className="text-xs text-gray-400 tabular-nums">
              {alerts.length} total
            </span>
          </div>
        </div>

        {/* Alert list */}
        {sorted.length === 0 ? (
          <div className="px-5 py-8 text-center text-sm text-gray-400">
            No active alerts.
          </div>
        ) : (
          <>
            <div className="divide-y divide-gray-100">
              {sorted.map(alert => (
                <AlertRow
                  key={alert.id}
                  alert={alert}
                  selected={selectedAlert?.id === alert.id}
                  onClick={() => setSelectedAlert(prev => prev?.id === alert.id ? null : alert)}
                />
              ))}
            </div>
            <div className="px-5 py-2 border-t border-gray-100 bg-gray-50">
              <p className="text-[11px] text-gray-400 text-center">
                Click an alert to view incident details
              </p>
            </div>
          </>
        )}
      </div>

      {/* Slide-over detail panel */}
      {selectedAlert && (
        <IncidentDetailPanel
          alert={selectedAlert}
          integration={relatedIntegration}
          onClose={() => setSelectedAlert(null)}
        />
      )}
    </>
  );
}

// ── AlertRow ───────────────────────────────────────────────────────────────────

function AlertRow({
  alert,
  selected,
  onClick,
}: {
  alert:    SystemAlert;
  selected: boolean;
  onClick:  () => void;
}) {
  const cfg  = SEVERITY_CONFIG[alert.severity];
  const icon = SEVERITY_ICONS[alert.severity];
  const time = formatTimestamp(alert.createdAtUtc);

  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={selected}
      className={[
        'w-full text-left flex items-start gap-3.5 px-5 py-3.5',
        cfg.bg,
        'border-l-4 border-t-0 border-r-0 border-b-0',
        selected ? cfg.border + ' ring-1 ring-inset ring-gray-300' : cfg.border,
        'hover:brightness-95 transition-all cursor-pointer',
      ].join(' ')}
    >
      <span className="text-base leading-none mt-0.5 shrink-0" aria-hidden>
        {icon}
      </span>

      <div className="flex-1 min-w-0">
        <p className="text-sm text-gray-800 leading-snug">{alert.message}</p>
        <p className="text-xs text-gray-400 mt-1">
          {alert.entityName && (
            <span className="font-medium text-gray-500">{alert.entityName} · </span>
          )}
          {time}
        </p>
      </div>

      <div className="flex items-center gap-2 shrink-0">
        <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${cfg.badge}`}>
          {alert.severity}
        </span>
        <svg
          className="h-3.5 w-3.5 text-gray-400"
          viewBox="0 0 16 16"
          fill="none"
          stroke="currentColor"
          strokeWidth={2}
          aria-hidden
        >
          <path d="M6 3l5 5-5 5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>
    </button>
  );
}

// ── helpers ────────────────────────────────────────────────────────────────────

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
      timeZone: 'UTC',
      timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}
