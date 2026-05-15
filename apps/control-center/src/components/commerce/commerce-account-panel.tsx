'use client';

import type { CommerceAccountSummary, CommerceAccountItem } from '@/types/control-center';

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

function AccountRow({ account }: { account: CommerceAccountItem }) {
  return (
    <div className="flex items-center gap-4 py-3 border-b border-slate-100 last:border-0">
      <div className="flex-1 min-w-0">
        <div className="text-sm font-medium text-slate-800 truncate">{account.displayName}</div>
        <div className="text-xs text-slate-400 font-mono mt-0.5">{account.accountNumber}</div>
      </div>
      <div className="flex items-center gap-2 shrink-0">
        <Pill value={account.status}  colorClass={statusColor(account.status)} />
        <Pill value={account.standing} colorClass={standingColor(account.standing)} />
      </div>
      {account.standingReason && (
        <div className="text-xs text-slate-500 max-w-[200px] truncate hidden xl:block" title={account.standingReason}>
          {account.standingReason}
        </div>
      )}
    </div>
  );
}

export function CommerceAccountPanel({ summary, error }: Props) {
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
            <div className="divide-y divide-slate-100">
              {summary.accounts.map(acct => (
                <AccountRow key={acct.id} account={acct} />
              ))}
            </div>
            {summary.accountCount > 20 && (
              <div className="mt-3 text-xs text-slate-400 text-center">
                Showing 20 of {summary.accountCount} accounts.
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
