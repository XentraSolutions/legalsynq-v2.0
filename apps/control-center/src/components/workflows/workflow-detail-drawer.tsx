'use client';

import { useEffect, useId, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import type {
  WorkflowAdminAction,
  WorkflowInstanceDetail,
} from '@/types/control-center';

interface WorkflowDetailDrawerProps {
  /**
   * The currently selected workflow id (driven by the `?selected=` URL
   * param). When `null` the drawer is closed.
   */
  selectedId: string | null;

  /**
   * Server-fetched detail for `selectedId`, or `null` if the lookup
   * returned 404 / forbidden / scoped-out.
   */
  detail: WorkflowInstanceDetail | null;

  /**
   * Optional fetch error string (set by the parent page when the
   * server-side detail call threw). Drawer renders an error state
   * without breaking the underlying list.
   */
  errorMessage?: string | null;

  /**
   * E10.1 — when true, the drawer renders the Admin Actions section
   * (Retry / Force Complete / Cancel). The parent page is responsible
   * for asserting PlatformAdmin role server-side before passing this
   * flag; the BFF route re-asserts on submit.
   */
  canAdmin?: boolean;
}

const STATUS_STYLES: Record<string, string> = {
  Active:    'bg-blue-50   text-blue-700   border-blue-200',
  Pending:   'bg-amber-50  text-amber-700  border-amber-200',
  Completed: 'bg-green-50  text-green-700  border-green-200',
  Cancelled: 'bg-gray-100  text-gray-500   border-gray-200',
  Failed:    'bg-red-50    text-red-700    border-red-200',
};

const PRODUCT_LABELS: Record<string, string> = {
  FLOW_GENERIC:     'Flow',
  SYNQ_LIENS:       'SynqLien',
  SYNQ_LIEN:        'SynqLien',
  SYNQ_FUND:        'SynqFund',
  SYNQ_BILL:        'SynqBill',
  SYNQ_RX:          'SynqRx',
  SYNQ_PAYOUT:      'SynqPayout',
  SYNQ_CARECONNECT: 'CareConnect',
  CARE_CONNECT:     'CareConnect',
};

/**
 * E9.2 — Map (productKey, sourceEntityType, sourceEntityId) → tenant
 * portal route.
 *
 * Returns a relative path opened in a new tab. The tenant portal lives
 * at a different origin in production; relative navigation will resolve
 * within whichever tenant-portal subdomain the operator pastes/visits.
 * This is intentionally conservative: only product/entity combinations
 * with a verified route shape are surfaced. Everything else returns
 * null and the drawer just shows the entity id as text.
 */
function deriveSourceRecordHref(
  productKey: string | null | undefined,
  sourceEntityType: string | null | undefined,
  sourceEntityId: string | null | undefined,
): string | null {
  if (!sourceEntityId) return null;
  const type = (sourceEntityType ?? '').toLowerCase();
  const product = (productKey ?? '').toUpperCase();

  // SynqLien — case detail
  if (product === 'SYNQ_LIENS' || product === 'SYNQ_LIEN') {
    if (type === 'lien_case' || type === 'case' || type === 'lien-case') {
      return `/lien/cases/${encodeURIComponent(sourceEntityId)}`;
    }
  }

  // SynqFund — funding application detail
  if (product === 'SYNQ_FUND') {
    if (type === 'fund_application' || type === 'application' || type === 'funding_application') {
      return `/fund/applications/${encodeURIComponent(sourceEntityId)}`;
    }
  }

  // CareConnect — referral detail
  if (product === 'SYNQ_CARECONNECT' || product === 'CARE_CONNECT') {
    if (type === 'referral' || type === 'careconnect_referral') {
      return `/careconnect/referrals/${encodeURIComponent(sourceEntityId)}`;
    }
  }

  return null;
}

function formatTimestamp(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    year:   'numeric',
    hour:   '2-digit',
    minute: '2-digit',
  });
}

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_STYLES[status] ?? 'bg-gray-100 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${cls}`}>
      {status || 'Unknown'}
    </span>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-[11px] uppercase tracking-wide text-gray-400 font-semibold">{label}</dt>
      <dd className="mt-0.5 text-sm text-gray-800 break-words">{children}</dd>
    </div>
  );
}

/**
 * E10.1 — client-side eligibility for an admin action. Mirrors the
 * server-side state matrix on AdminWorkflowInstancesController so the
 * UI never offers a button the backend would reject. The server
 * remains the source of truth (any client/server divergence surfaces
 * as a `not_allowed_in_state` ProblemDetails banner inside the drawer
 * rather than silent corruption).
 */
function isActionEligible(
  action: WorkflowAdminAction,
  detail: WorkflowInstanceDetail | null,
): boolean {
  if (!detail) return false;
  const s = detail.status;
  const hasErr = !!(detail.lastErrorMessage && detail.lastErrorMessage.length > 0);
  switch (action) {
    case 'retry':
      return s === 'Failed' || ((s === 'Active' || s === 'Pending') && hasErr);
    case 'force-complete':
    case 'cancel':
      return s === 'Active' || s === 'Pending';
    default:
      return false;
  }
}

const ACTION_LABELS: Record<WorkflowAdminAction, string> = {
  'retry':          'Retry',
  'force-complete': 'Force complete',
  'cancel':         'Cancel',
};

const ACTION_INELIGIBLE_HINT: Record<WorkflowAdminAction, string> = {
  'retry':          'Retry is only available for Failed workflows or Active/Pending workflows with a captured error.',
  'force-complete': 'Force complete is only available for Active or Pending workflows.',
  'cancel':         'Cancel is only available for Active or Pending workflows.',
};

const REASON_MAX = 1000;

export function WorkflowDetailDrawer({
  selectedId,
  detail,
  errorMessage,
  canAdmin = false,
}: WorkflowDetailDrawerProps) {
  const titleId = useId();
  const router  = useRouter();
  const params  = useSearchParams();

  const isOpen = !!selectedId;

  // E10.1 — admin action panel state. `pendingAction` controls which
  // confirmation panel is open; only one can be active at a time so
  // operators cannot accidentally fire two state transitions in
  // rapid succession.
  const [pendingAction, setPendingAction] = useState<WorkflowAdminAction | null>(null);
  const [reason,        setReason]        = useState('');
  const [submitting,    setSubmitting]    = useState(false);
  const [actionError,   setActionError]   = useState<string | null>(null);

  // Reset action panel whenever the drawer changes target.
  useEffect(() => {
    setPendingAction(null);
    setReason('');
    setSubmitting(false);
    setActionError(null);
  }, [selectedId]);

  // Build the close target: same URL minus `?selected=`. Preserves all
  // other filter params so the operator returns to the same list view.
  const closeHref = useMemo(() => {
    const next = new URLSearchParams(params?.toString() ?? '');
    next.delete('selected');
    const qs = next.toString();
    return qs ? `?${qs}` : '?';
  }, [params]);

  function handleClose() {
    router.replace(closeHref, { scroll: false });
  }

  // ESC-to-close (parity with the Tenants modal pattern).
  useEffect(() => {
    if (!isOpen) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') handleClose();
    }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, closeHref]);

  if (!isOpen) return null;

  const productLabel  = detail ? (PRODUCT_LABELS[detail.productKey] ?? detail.productKey) : '—';
  const sourceHref    = detail
    ? deriveSourceRecordHref(detail.productKey, detail.sourceEntityType, detail.sourceEntityId)
    : null;

  return (
    <div
      className="fixed inset-0 z-50 flex"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
    >
      {/* Backdrop */}
      <button
        type="button"
        aria-label="Close workflow detail"
        onClick={handleClose}
        className="flex-1 bg-gray-900/30 backdrop-blur-[1px] cursor-default"
      />

      {/* Panel */}
      <aside className="w-full sm:w-[28rem] md:w-[32rem] bg-white border-l border-gray-200 shadow-xl flex flex-col">
        {/* Header */}
        <header className="px-5 py-4 border-b border-gray-100 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-[11px] uppercase tracking-wide text-gray-400 font-semibold">Workflow detail</p>
            <h2 id={titleId} className="mt-0.5 text-base font-semibold text-gray-900 truncate">
              {detail?.workflowName ?? (selectedId ? 'Loading…' : 'Workflow')}
            </h2>
            <p className="mt-0.5 text-[11px] text-gray-400 font-mono truncate">{selectedId}</p>
          </div>
          <button
            type="button"
            onClick={handleClose}
            className="rounded-md p-1.5 text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors"
            aria-label="Close"
          >
            <i className="ri-close-line text-lg leading-none" aria-hidden="true" />
          </button>
        </header>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-5 space-y-6">
          {errorMessage && (
            <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
              {errorMessage}
            </div>
          )}

          {!errorMessage && !detail && (
            <div className="bg-amber-50 border border-amber-200 rounded-md px-3 py-2 text-sm text-amber-800">
              This workflow is no longer available, or you do not have visibility into it.
            </div>
          )}

          {detail && (
            <>
              {/* Summary */}
              <section className="space-y-3">
                <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Summary</h3>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
                  <Field label="Status"><StatusBadge status={detail.status} /></Field>
                  <Field label="Product">{productLabel}</Field>
                  <Field label="Current step">
                    {detail.currentStepName ?? detail.currentStepKey ?? '—'}
                    {detail.currentStepName && detail.currentStepKey && (
                      <span className="ml-1 text-[11px] text-gray-400 font-mono">({detail.currentStepKey})</span>
                    )}
                  </Field>
                  <Field label="Tenant">
                    <span className="font-mono text-[12px]">{detail.tenantId}</span>
                  </Field>
                </dl>
              </section>

              {/* Source Context */}
              <section className="space-y-3">
                <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Source Context</h3>
                <dl className="space-y-3">
                  <Field label="Source entity">
                    {detail.sourceEntityType
                      ? (
                        <div className="space-y-1">
                          <p>{detail.sourceEntityType}</p>
                          <p className="font-mono text-[12px] text-gray-500 break-all">{detail.sourceEntityId ?? '—'}</p>
                          {sourceHref && (
                            <a
                              href={sourceHref}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="inline-flex items-center gap-1 text-xs text-indigo-600 hover:underline"
                            >
                              Open source record
                              <i className="ri-external-link-line text-[12px]" aria-hidden="true" />
                            </a>
                          )}
                        </div>
                      )
                      : <span className="text-gray-400">—</span>}
                  </Field>
                  <Field label="Correlation key">
                    {detail.correlationKey ?? <span className="text-gray-400">—</span>}
                  </Field>
                </dl>
              </section>

              {/* Lifecycle */}
              <section className="space-y-3">
                <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Lifecycle</h3>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
                  <Field label="Started">{formatTimestamp(detail.startedAt)}</Field>
                  <Field label="Updated">{formatTimestamp(detail.updatedAt ?? detail.createdAt)}</Field>
                  <Field label="Completed">{formatTimestamp(detail.completedAt)}</Field>
                  <Field label="Assigned">
                    {detail.assignedToUserId
                      ? <span className="font-mono text-[12px]">{detail.assignedToUserId}</span>
                      : <span className="text-gray-400">—</span>}
                  </Field>
                </dl>
              </section>

              {/* E10.1 — Admin Actions */}
              {canAdmin && (
                <section className="space-y-3" data-testid="workflow-admin-actions">
                  <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Admin actions</h3>
                  <p className="text-[11px] text-gray-500">
                    Override the workflow engine. Every action is recorded with your reason in the audit log.
                  </p>

                  <div className="flex flex-wrap gap-2">
                    {(['retry', 'force-complete', 'cancel'] as WorkflowAdminAction[]).map((a) => {
                      const eligible = isActionEligible(a, detail);
                      const isOpenForThis = pendingAction === a;
                      const tone =
                        a === 'cancel'
                          ? 'border-red-300 text-red-700 hover:bg-red-50'
                          : a === 'force-complete'
                            ? 'border-amber-300 text-amber-800 hover:bg-amber-50'
                            : 'border-indigo-300 text-indigo-700 hover:bg-indigo-50';
                      return (
                        <button
                          key={a}
                          type="button"
                          disabled={!eligible || submitting}
                          aria-pressed={isOpenForThis}
                          title={eligible ? '' : ACTION_INELIGIBLE_HINT[a]}
                          onClick={() => {
                            if (!eligible) return;
                            setActionError(null);
                            setReason('');
                            setPendingAction(isOpenForThis ? null : a);
                          }}
                          className={`text-xs font-medium px-2.5 py-1.5 rounded-md border bg-white transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${tone} ${isOpenForThis ? 'ring-2 ring-offset-1 ring-indigo-300' : ''}`}
                        >
                          {ACTION_LABELS[a]}
                        </button>
                      );
                    })}
                  </div>

                  {pendingAction && (
                    <div className="rounded-md border border-gray-200 bg-gray-50 p-3 space-y-2">
                      <label className="block text-[11px] uppercase tracking-wide text-gray-500 font-semibold" htmlFor={`${titleId}-reason`}>
                        Reason for {ACTION_LABELS[pendingAction].toLowerCase()}
                      </label>
                      <textarea
                        id={`${titleId}-reason`}
                        value={reason}
                        onChange={(e) => setReason(e.target.value.slice(0, REASON_MAX))}
                        rows={3}
                        maxLength={REASON_MAX}
                        placeholder="Required. Recorded in the audit log."
                        className="w-full rounded-md border border-gray-300 bg-white px-2 py-1.5 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-indigo-200 focus:border-indigo-300"
                      />
                      <div className="flex items-center justify-between text-[11px] text-gray-400">
                        <span>{reason.length} / {REASON_MAX}</span>
                        <span>This action cannot be undone.</span>
                      </div>

                      {actionError && (
                        <div className="bg-red-50 border border-red-200 rounded px-2 py-1.5 text-xs text-red-700">
                          {actionError}
                        </div>
                      )}

                      <div className="flex items-center justify-end gap-2 pt-1">
                        <button
                          type="button"
                          onClick={() => { setPendingAction(null); setReason(''); setActionError(null); }}
                          disabled={submitting}
                          className="text-xs px-2.5 py-1.5 rounded-md border border-gray-200 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-40"
                        >
                          Dismiss
                        </button>
                        <button
                          type="button"
                          disabled={submitting || reason.trim().length === 0 || !detail}
                          onClick={async () => {
                            if (!detail) return;
                            const trimmed = reason.trim();
                            if (trimmed.length === 0) {
                              setActionError('A reason is required.');
                              return;
                            }
                            setSubmitting(true);
                            setActionError(null);
                            try {
                              const res = await fetch(
                                `/api/admin/workflow-instances/${encodeURIComponent(detail.id)}/${pendingAction}`,
                                {
                                  method:  'POST',
                                  headers: { 'Content-Type': 'application/json' },
                                  body:    JSON.stringify({ reason: trimmed }),
                                },
                              );
                              if (!res.ok) {
                                let msg = `Action failed (HTTP ${res.status}).`;
                                try {
                                  const body = (await res.json()) as { error?: string; detail?: string; title?: string };
                                  msg = body.error ?? body.detail ?? body.title ?? msg;
                                } catch { /* keep default */ }
                                setActionError(msg);
                                setSubmitting(false);
                                return;
                              }
                              setSubmitting(false);
                              setPendingAction(null);
                              setReason('');
                              router.refresh();
                            } catch (err) {
                              setActionError(err instanceof Error ? err.message : 'Network error.');
                              setSubmitting(false);
                            }
                          }}
                          className="text-xs px-2.5 py-1.5 rounded-md bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          {submitting ? 'Applying…' : `Confirm ${ACTION_LABELS[pendingAction].toLowerCase()}`}
                        </button>
                      </div>
                    </div>
                  )}
                </section>
              )}

              {/* Diagnostics */}
              {(detail.lastErrorMessage || detail.status === 'Failed' || detail.status === 'Cancelled') && (
                <section className="space-y-3">
                  <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Diagnostics</h3>
                  {detail.lastErrorMessage ? (
                    <pre className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-xs text-red-700 whitespace-pre-wrap break-words">
                      {detail.lastErrorMessage}
                    </pre>
                  ) : (
                    <p className="text-xs text-gray-500">
                      No engine error captured. Workflow ended in <strong>{detail.status}</strong> state.
                    </p>
                  )}
                </section>
              )}
            </>
          )}
        </div>

        {/* Footer */}
        <footer className="px-5 py-3 border-t border-gray-100 text-[11px] text-gray-400">
          {canAdmin
            ? 'Admin overrides bypass the workflow engine. Use sparingly.'
            : 'Read-only inspection. Execution actions remain on the product surface.'}
        </footer>
      </aside>
    </div>
  );
}
