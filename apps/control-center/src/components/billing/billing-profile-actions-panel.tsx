'use client';

import { useState } from 'react';
import type { BillingProfileActionResult } from '@/types/control-center';

interface Props {
  profileId:     string;
  currentStatus: string;
  tenantId?:     string;
  onActionComplete?: (result: BillingProfileActionResult) => void;
}

type ActionType = 'activate' | 'suspend' | 'close';

interface ActionConfig {
  label:       string;
  description: string;
  icon:        string;
  buttonClass: string;
  allowed:     string[];
}

const ACTION_CONFIG: Record<ActionType, ActionConfig> = {
  activate: {
    label:       'Activate',
    description: 'Enable the billing profile so the resolver returns this account for the tenant.',
    icon:        'ri-checkbox-circle-line',
    buttonClass: 'bg-emerald-600 hover:bg-emerald-700 text-white',
    allowed:     ['Draft', 'Suspended'],
  },
  suspend: {
    label:       'Suspend',
    description: 'Temporarily pause this profile. The resolver will return null while suspended. Reversible.',
    icon:        'ri-pause-circle-line',
    buttonClass: 'bg-amber-600 hover:bg-amber-700 text-white',
    allowed:     ['Active'],
  },
  close: {
    label:       'Close',
    description: 'Permanently retire this profile. This cannot be undone. A new profile may be created for the same tenant.',
    icon:        'ri-close-circle-line',
    buttonClass: 'bg-red-600 hover:bg-red-700 text-white',
    allowed:     ['Draft', 'Active', 'Suspended'],
  },
};

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'active')    return 'bg-emerald-100 text-emerald-800 border-emerald-300';
  if (s === 'suspended') return 'bg-amber-100  text-amber-800  border-amber-300';
  if (s === 'closed')    return 'bg-red-100    text-red-800    border-red-300';
  if (s === 'draft')     return 'bg-slate-100  text-slate-600  border-slate-300';
  return 'bg-slate-100 text-slate-500 border-slate-200';
}

export function BillingProfileActionsPanel({
  profileId,
  currentStatus,
  tenantId,
  onActionComplete,
}: Props) {
  const [pending,    setPending]    = useState<ActionType | null>(null);
  const [confirming, setConfirming] = useState<ActionType | null>(null);
  const [lastResult, setLastResult] = useState<BillingProfileActionResult | null>(null);
  const [status,     setStatus]     = useState(currentStatus);

  const availableActions = (Object.keys(ACTION_CONFIG) as ActionType[]).filter(
    action => ACTION_CONFIG[action].allowed.includes(status),
  );

  async function execute(action: ActionType) {
    setPending(action);
    setConfirming(null);
    setLastResult(null);

    try {
      const res = await fetch(
        `/api/billing/profiles/${encodeURIComponent(profileId)}/${action}`,
        { method: 'POST', credentials: 'include' },
      );
      const result: BillingProfileActionResult = await res.json();
      setLastResult(result);
      if (result.success && result.newStatus) {
        setStatus(result.newStatus);
      }
      onActionComplete?.(result);
    } catch {
      setLastResult({
        success:       false,
        action,
        profileId,
        error:         'Request failed — unable to reach server.',
        executedAtUtc: new Date().toISOString(),
      });
    } finally {
      setPending(null);
    }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 bg-slate-50 border-b border-slate-200 flex items-center gap-2">
        <i className="ri-settings-4-line text-indigo-500" />
        <h2 className="text-sm font-semibold text-slate-700">Profile Lifecycle Actions</h2>
        <span className={`ml-auto inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold border ${statusColor(status)}`}>
          {status}
        </span>
      </div>

      <div className="p-5 space-y-4">
        {tenantId && (
          <div className="text-xs text-slate-400 font-mono truncate">
            Profile: {profileId}
          </div>
        )}

        {lastResult && (
          <div className={`flex items-start gap-2 rounded-md px-4 py-3 text-sm ${
            lastResult.success
              ? 'bg-emerald-50 border border-emerald-200 text-emerald-800'
              : 'bg-red-50 border border-red-200 text-red-700'
          }`}>
            <i className={`${lastResult.success ? 'ri-checkbox-circle-line' : 'ri-error-warning-line'} mt-0.5 shrink-0`} />
            <div>
              {lastResult.success
                ? <>Action <strong>{lastResult.action}</strong> applied successfully. New status: <strong>{lastResult.newStatus ?? '—'}</strong></>
                : <>{lastResult.error ?? `Action ${lastResult.action} failed.`}</>
              }
            </div>
          </div>
        )}

        {availableActions.length === 0 ? (
          <div className="flex items-center gap-2 text-sm text-slate-400 py-2">
            <i className="ri-information-line" />
            {status.toLowerCase() === 'closed'
              ? 'This profile is closed. No further lifecycle actions are available.'
              : 'No lifecycle actions are available for the current profile state.'}
          </div>
        ) : (
          <div className="space-y-3">
            {availableActions.map(action => {
              const cfg = ACTION_CONFIG[action];
              const isConfirming = confirming === action;
              const isRunning    = pending === action;

              return (
                <div
                  key={action}
                  className="flex items-start gap-4 p-4 rounded-lg border border-slate-200 bg-slate-50"
                >
                  <i className={`${cfg.icon} text-lg text-slate-500 mt-0.5 shrink-0`} />
                  <div className="flex-1 min-w-0">
                    <div className="text-sm font-semibold text-slate-700">{cfg.label}</div>
                    <div className="text-xs text-slate-500 mt-0.5">{cfg.description}</div>

                    {isConfirming && (
                      <div className="mt-3 flex items-center gap-2">
                        <span className="text-xs text-slate-600 font-medium">Confirm {cfg.label.toLowerCase()}?</span>
                        <button
                          onClick={() => execute(action)}
                          disabled={!!pending}
                          className={`px-3 py-1.5 rounded text-xs font-semibold transition-colors disabled:opacity-50 ${cfg.buttonClass}`}
                        >
                          {isRunning ? 'Running…' : `Yes, ${cfg.label}`}
                        </button>
                        <button
                          onClick={() => setConfirming(null)}
                          disabled={!!pending}
                          className="px-3 py-1.5 rounded text-xs font-semibold bg-slate-200 hover:bg-slate-300 text-slate-700 transition-colors disabled:opacity-50"
                        >
                          Cancel
                        </button>
                      </div>
                    )}
                  </div>

                  {!isConfirming && (
                    <button
                      onClick={() => setConfirming(action)}
                      disabled={!!pending}
                      className="shrink-0 px-3 py-1.5 rounded text-xs font-semibold border border-slate-300 bg-white hover:bg-slate-100 text-slate-700 transition-colors disabled:opacity-50"
                    >
                      {cfg.label}
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        )}

        <p className="text-xs text-slate-400 border-t border-slate-100 pt-3">
          PlatformAdmin only. All actions are permanent or reversible as described above.
          Page guards enforce authorization server-side.
        </p>
      </div>
    </section>
  );
}
