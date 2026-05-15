'use client';

import { useState } from 'react';
import type { CommerceAccountSummary, CommerceAccountItem, EntitlementPublishResult } from '@/types/control-center';
import { CommerceSubscriptionsPanel }        from './commerce-subscriptions-panel';
import { BillingAccountAuditPanel }          from './billing-account-audit-panel';
import { EntitlementReconciliationPanel }    from './entitlement-reconciliation-panel';

interface Props {
  summary: CommerceAccountSummary | null;
  error?:  string | null;
}

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'active')    return 'bg-emerald-100 text-emerald-800';
  if (s === 'suspended') return 'bg-amber-100  text-amber-800';
  if (s === 'closed')    return 'bg-slate-100  text-slate-500';
  return 'bg-slate-100 text-slate-500';
}

function standingColor(standing: string): string {
  const s = standing.toLowerCase();
  if (s === 'good')      return 'bg-emerald-100 text-emerald-800';
  if (s === 'warning')   return 'bg-amber-100  text-amber-800';
  if (s === 'suspended') return 'bg-orange-100 text-orange-800';
  if (s === 'blocked')   return 'bg-red-100    text-red-800';
  return 'bg-slate-100 text-slate-500';
}

function Pill({ value, colorClass }: { value: string; colorClass: string }) {
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold ${colorClass}`}>
      {value}
    </span>
  );
}

function fmt(iso: string | null | undefined): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

const STATUS_FILTERS  = ['All', 'Active', 'Suspended', 'Closed'];
const STANDING_FILTERS = ['All', 'Good', 'Warning', 'Suspended', 'Blocked'];

function PublishButton({ accountId, accountName }: { accountId: string; accountName: string }) {
  const [confirming, setConfirming] = useState(false);
  const [running,    setRunning]    = useState(false);
  const [result,     setResult]     = useState<EntitlementPublishResult | null>(null);

  async function publish() {
    setRunning(true);
    setConfirming(false);
    setResult(null);
    try {
      const res = await fetch(
        `/api/commerce/billing-accounts/${encodeURIComponent(accountId)}/publish-entitlement`,
        { method: 'POST', credentials: 'include' },
      );
      const data: EntitlementPublishResult = await res.json();
      setResult(data);
    } catch {
      setResult({
        outcome: 'failed', billingAccountId: accountId, tenantId: null,
        httpStatus: null, reason: 'Request failed.', attempts: 0,
        executedAtUtc: new Date().toISOString(),
        error: 'Request failed — unable to reach server.',
      });
    } finally {
      setRunning(false);
    }
  }

  if (result) {
    const ok = result.outcome === 'published';
    return (
      <div className={`mt-2 flex items-center gap-2 text-xs rounded px-3 py-1.5 ${ok ? 'bg-emerald-50 text-emerald-800 border border-emerald-200' : 'bg-amber-50 text-amber-800 border border-amber-200'}`}>
        <i className={ok ? 'ri-checkbox-circle-line' : 'ri-information-line'} />
        <span>{ok ? `Published (${result.attempts} attempt${result.attempts !== 1 ? 's' : ''})` : (result.error ?? result.reason)}</span>
        <button onClick={() => setResult(null)} className="ml-auto text-slate-400 hover:text-slate-600">
          <i className="ri-close-line" />
        </button>
      </div>
    );
  }

  if (confirming) {
    return (
      <div className="mt-2 flex items-center gap-2 text-xs">
        <span className="text-slate-600">Publish entitlement for <strong>{accountName}</strong>?</span>
        <button onClick={publish} disabled={running} className="px-2.5 py-1 rounded bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors">
          {running ? 'Publishing…' : 'Confirm'}
        </button>
        <button onClick={() => setConfirming(false)} disabled={running} className="px-2.5 py-1 rounded bg-slate-200 text-slate-700 hover:bg-slate-300 disabled:opacity-50 transition-colors">
          Cancel
        </button>
      </div>
    );
  }

  return (
    <button
      onClick={() => setConfirming(true)}
      disabled={running}
      title="Trigger Commerce → Tenant Billing entitlement publish"
      className="shrink-0 flex items-center gap-1 text-xs px-2.5 py-1 rounded border border-indigo-300 text-indigo-700 hover:bg-indigo-50 disabled:opacity-50 transition-colors"
    >
      <i className="ri-send-plane-line" />
      Publish
    </button>
  );
}

type ExpandPane = 'subs' | 'audit' | 'reconcile' | null;

interface PaneToggleProps {
  pane:     ExpandPane;
  icon:     string;
  label:    string;
  openPane: ExpandPane;
  onToggle: (pane: ExpandPane) => void;
}

function PaneToggle({ pane, icon, label, openPane, onToggle }: PaneToggleProps) {
  const active = openPane === pane;
  return (
    <button
      onClick={() => onToggle(pane)}
      className={`shrink-0 flex items-center gap-1 text-xs px-2.5 py-1 rounded border transition-colors ${
        active
          ? 'bg-indigo-50 border-indigo-300 text-indigo-700'
          : 'border-slate-200 text-slate-500 hover:bg-slate-50'
      }`}
      title={label}
    >
      <i className={icon} />
      {label}
    </button>
  );
}

function AccountRow({ account }: { account: CommerceAccountItem }) {
  const [openPane, setOpenPane] = useState<ExpandPane>(null);

  function togglePane(pane: ExpandPane) {
    setOpenPane(v => v === pane ? null : pane);
  }

  return (
    <div className="py-3 border-b border-slate-100 last:border-0">
      <div className="flex items-center gap-3 flex-wrap">
        <div className="flex-1 min-w-0">
          <div className="text-sm font-medium text-slate-800 truncate">{account.displayName}</div>
          <div className="text-xs text-slate-400 font-mono mt-0.5">{account.accountNumber}</div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <Pill value={account.status}   colorClass={statusColor(account.status)} />
          <Pill value={account.standing} colorClass={standingColor(account.standing)} />
        </div>
        {account.standingReason && (
          <div className="text-xs text-slate-500 max-w-[160px] truncate hidden xl:block" title={account.standingReason}>
            {account.standingReason}
          </div>
        )}
        <div className="flex items-center gap-1.5 shrink-0">
          <PaneToggle pane="subs"      icon={`ri-arrow-${openPane === 'subs'      ? 'up' : 'down'}-s-line`} label="Subs"      openPane={openPane} onToggle={togglePane} />
          <PaneToggle pane="audit"     icon="ri-file-list-3-line"                                           label="Audit"     openPane={openPane} onToggle={togglePane} />
          <PaneToggle pane="reconcile" icon="ri-git-merge-line"                                             label="Reconcile" openPane={openPane} onToggle={togglePane} />
          <PublishButton accountId={account.id} accountName={account.displayName} />
        </div>
      </div>

      {openPane === 'subs' && (
        <div className="mt-3">
          <CommerceSubscriptionsPanel billingAccountId={account.id} accountName={account.displayName} />
        </div>
      )}
      {openPane === 'audit' && (
        <div className="mt-3">
          <BillingAccountAuditPanel billingAccountId={account.id} accountName={account.displayName} />
        </div>
      )}
      {openPane === 'reconcile' && (
        <div className="mt-3">
          <EntitlementReconciliationPanel billingAccountId={account.id} accountName={account.displayName} />
        </div>
      )}
    </div>
  );
}

export function CommerceAccountPanel({ summary, error }: Props) {
  const [search,          setSearch]          = useState('');
  const [statusFilter,    setStatusFilter]    = useState('All');
  const [standingFilter,  setStandingFilter]  = useState('All');

  const allAccounts = summary?.accounts ?? [];

  const filtered = allAccounts.filter(a => {
    const matchSearch = !search ||
      a.displayName.toLowerCase().includes(search.toLowerCase()) ||
      a.accountNumber.toLowerCase().includes(search.toLowerCase());
    const matchStatus  = statusFilter  === 'All' || a.status.toLowerCase()  === statusFilter.toLowerCase();
    const matchStanding = standingFilter === 'All' || a.standing.toLowerCase() === standingFilter.toLowerCase();
    return matchSearch && matchStatus && matchStanding;
  });

  return (
    <section className="bg-white border border-slate-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 bg-slate-50 border-b border-slate-200 flex items-center gap-2">
        <i className="ri-building-4-line text-indigo-500" />
        <h2 className="text-sm font-semibold text-slate-700">Billing Accounts</h2>
        {summary && summary.error === null && (
          <span className="ml-auto text-xs text-slate-500">
            {summary.accountCount} account{summary.accountCount !== 1 ? 's' : ''}
          </span>
        )}
      </div>

      <div className="p-5">
        {error && (
          <div className="flex items-start gap-2 rounded-md bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700 mb-4">
            <i className="ri-error-warning-line mt-0.5 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {summary?.error && (
          <div className="flex items-start gap-2 rounded-md bg-amber-50 border border-amber-200 px-4 py-3 text-sm text-amber-800 mb-4">
            <i className="ri-information-line mt-0.5 shrink-0" />
            <span>{summary.error}</span>
          </div>
        )}

        {!summary && !error && (
          <div className="flex items-center gap-2 text-sm text-slate-400 py-4">
            <i className="ri-information-line" />
            Billing account data unavailable.
          </div>
        )}

        {summary && !summary.error && summary.accounts.length === 0 && (
          <div className="flex items-center gap-2 text-sm text-slate-400 py-4">
            <i className="ri-inbox-line" />
            No billing accounts found.
          </div>
        )}

        {summary && summary.accounts.length > 0 && (
          <>
            {/* ── Search & filter bar ── */}
            <div className="flex flex-wrap gap-3 mb-4">
              <div className="relative flex-1 min-w-[180px]">
                <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-sm" />
                <input
                  type="text"
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  placeholder="Search name or account #…"
                  className="w-full pl-8 pr-3 py-1.5 text-sm border border-slate-200 rounded-md focus:outline-none focus:ring-1 focus:ring-indigo-400"
                />
              </div>

              <div className="flex items-center gap-1 flex-wrap">
                {STATUS_FILTERS.map(f => (
                  <button
                    key={f}
                    onClick={() => setStatusFilter(f)}
                    className={`px-2.5 py-1 rounded-full text-xs font-medium transition-colors ${
                      statusFilter === f
                        ? 'bg-indigo-100 text-indigo-700 border border-indigo-300'
                        : 'bg-slate-100 text-slate-600 border border-slate-200 hover:bg-slate-200'
                    }`}
                  >
                    {f}
                  </button>
                ))}
              </div>

              <div className="flex items-center gap-1 flex-wrap">
                {STANDING_FILTERS.map(f => (
                  <button
                    key={f}
                    onClick={() => setStandingFilter(f)}
                    className={`px-2.5 py-1 rounded-full text-xs font-medium transition-colors ${
                      standingFilter === f
                        ? 'bg-slate-700 text-white border border-slate-700'
                        : 'bg-slate-100 text-slate-600 border border-slate-200 hover:bg-slate-200'
                    }`}
                  >
                    {f}
                  </button>
                ))}
              </div>
            </div>

            {/* ── Account rows ── */}
            {filtered.length === 0 ? (
              <div className="flex items-center gap-2 text-sm text-slate-400 py-3">
                <i className="ri-filter-off-line" />
                No accounts match the current filter.
              </div>
            ) : (
              <div>
                {filtered.map(acct => (
                  <AccountRow key={acct.id} account={acct} />
                ))}
              </div>
            )}

            {summary.accountCount > 20 && (
              <div className="mt-3 text-xs text-slate-400 text-center">
                Showing 20 of {summary.accountCount} accounts. Use search to narrow results.
              </div>
            )}
            <div className="mt-3 text-xs text-slate-400">
              Last checked: {fmt(summary.lastCheckedAtUtc)}
            </div>
          </>
        )}
      </div>
    </section>
  );
}
