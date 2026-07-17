'use client';

import Link from 'next/link';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import { PageHeader } from '@/components/lien/page-header';
import { DateDisplay } from '@/components/ui/date-display';
import { lienSalesService } from '@/lib/liens/lien-sales.service';
import type {
  SellingPortfolioActivityDto,
  SellingPortfolioAnalyticsDto,
  SellingPortfolioDto,
  SellingPortfolioStatusHistoryDto,
} from '@/lib/liens/lien-sales.types';

function money(value?: number | null): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(value ?? 0);
}

export function LienSaleDetailClient({ id }: { id: string }) {
  const [portfolio, setPortfolio] = useState<SellingPortfolioDto | null>(null);
  const [analytics, setAnalytics] = useState<SellingPortfolioAnalyticsDto | null>(null);
  const [activity, setActivity] = useState<SellingPortfolioActivityDto[]>([]);
  const [statusHistory, setStatusHistory] = useState<SellingPortfolioStatusHistoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const result = await lienSalesService.getPortfolio(id);
      setPortfolio(result.portfolio);
      setAnalytics(result.analytics);
      setActivity(result.activity);
      setStatusHistory(result.statusHistory);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load sale portfolio.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, [id]);

  async function addLiens(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const values = String(form.get('liens') ?? '')
      .split(/[\n,]+/)
      .map(value => value.trim())
      .filter(Boolean);
    if (values.length === 0) return;

    setSaving(true);
    setError(null);
    try {
      await lienSalesService.addLiens(id, { liens: values });
      event.currentTarget.reset();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add liens.');
    } finally {
      setSaving(false);
    }
  }

  async function removeLien(lienId: string) {
    setSaving(true);
    setError(null);
    try {
      await lienSalesService.removeLiens(id, [lienId]);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove lien.');
    } finally {
      setSaving(false);
    }
  }

  async function transition(action: 'publish' | 'withdraw') {
    setSaving(true);
    setError(null);
    try {
      if (action === 'publish') await lienSalesService.publish(id, 'Published from seller portal');
      else await lienSalesService.withdraw(id, 'Withdrawn from seller portal');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : `Failed to ${action} portfolio.`);
    } finally {
      setSaving(false);
    }
  }

  const canEdit = portfolio?.status === 'DRAFT';
  const totals = useMemo(() => analytics?.financial, [analytics]);

  if (loading) return <div className="rounded-lg border border-gray-200 bg-white p-8 text-sm text-gray-400">Loading sale portfolio...</div>;

  if (error && !portfolio) {
    return (
      <div className="space-y-4">
        <PageHeader title="Sale Portfolio" breadcrumbs={[{ label: 'Lien Sales', href: '/lien/sales' }, { label: 'Error' }]} />
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      </div>
    );
  }

  if (!portfolio) return null;

  return (
    <div className="space-y-4">
      <PageHeader
        title={portfolio.name}
        subtitle={`${portfolio.portfolioNumber} · ${portfolio.status.replaceAll('_', ' ')}`}
        breadcrumbs={[{ label: 'Lien Sales', href: '/lien/sales' }, { label: portfolio.portfolioNumber }]}
        actions={(
          <>
            <button disabled={saving || !['DRAFT', 'READY_FOR_REVIEW'].includes(portfolio.status)} onClick={() => transition('publish')} className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-white hover:bg-primary/90 disabled:opacity-50">
              Publish
            </button>
            <button disabled={saving || portfolio.status === 'WITHDRAWN' || portfolio.status === 'CLOSED'} onClick={() => transition('withdraw')} className="rounded-md border border-gray-200 px-3 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50">
              Withdraw
            </button>
          </>
        )}
      />

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <Metric label="Liens" value={String(portfolio.lienCount)} />
        <Metric label="Receivables" value={money(totals?.totalReceivables ?? portfolio.originalAmountTotal)} />
        <Metric label="Outstanding" value={money(totals?.totalOutstandingBalance ?? portfolio.currentBalanceTotal)} />
        <Metric label="Avg Balance" value={money(totals?.averageLienBalance)} />
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
        <section className="space-y-4">
          <div className="rounded-lg border border-gray-200 bg-white p-4">
            <h2 className="text-sm font-semibold text-gray-900">Portfolio Builder</h2>
            <p className="mt-1 text-xs text-gray-500">Add existing lien IDs or lien numbers. Linked cases are reused through each lien's canonical case reference.</p>
            <form onSubmit={addLiens} className="mt-3 space-y-3">
              <textarea name="liens" disabled={!canEdit || saving} rows={3} placeholder="LIEN-001, LIEN-002, or one per line" className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm outline-none focus:border-primary disabled:bg-gray-50" />
              <button disabled={!canEdit || saving} className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-white hover:bg-primary/90 disabled:opacity-50">
                Add Liens
              </button>
            </form>
          </div>

          <div className="overflow-hidden rounded-lg border border-gray-200 bg-white">
            <div className="border-b border-gray-100 px-4 py-3">
              <h2 className="text-sm font-semibold text-gray-900">Included Liens</h2>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100">
                <thead className="bg-gray-50">
                  <tr>
                    {['Lien', 'Case', 'Subject', 'Balance', 'Status', ''].map(label => (
                      <th key={label} className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wide text-gray-500">{label}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {portfolio.liens.length === 0 && <tr><td colSpan={6} className="px-4 py-8 text-center text-sm text-gray-400">No liens have been added.</td></tr>}
                  {portfolio.liens.map(lien => {
                    const subject = `${lien.subjectFirstName ?? ''} ${lien.subjectLastName ?? ''}`.trim() || '-';
                    return (
                      <tr key={lien.id}>
                        <td className="px-4 py-3">
                          <Link href={`/lien/liens/${lien.lienId}`} className="text-xs font-mono text-primary hover:underline">{lien.lienNumber}</Link>
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-600">
                          {lien.caseId ? <Link href={`/lien/cases/${lien.caseId}`} className="text-primary hover:underline">{lien.caseExternalId ?? 'View Case'}</Link> : '-'}
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-700">{subject}</td>
                        <td className="px-4 py-3 text-sm font-medium text-gray-900">{money(lien.currentBalance)}</td>
                        <td className="px-4 py-3 text-sm text-gray-600">{lien.lienLifecycleStatus}</td>
                        <td className="px-4 py-3 text-right">
                          <button disabled={!canEdit || saving} onClick={() => removeLien(lien.lienId)} className="text-sm font-medium text-red-600 hover:underline disabled:text-gray-300">Remove</button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </section>

        <aside className="space-y-4">
          <Panel title="Aging">
            <div className="space-y-2">
              {analytics?.agingBuckets.map(bucket => (
                <div key={bucket.bucket}>
                  <div className="mb-1 flex items-center justify-between text-xs text-gray-500">
                    <span>{bucket.bucket}</span>
                    <span>{bucket.lienCount} · {money(bucket.outstandingBalance)}</span>
                  </div>
                  <div className="h-2 rounded-full bg-gray-100">
                    <div className="h-2 rounded-full bg-primary" style={{ width: `${Math.min(100, bucket.lienCount * 18)}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </Panel>

          <Panel title="Lifecycle">
            <ol className="space-y-3">
              {statusHistory.map(item => (
                <li key={item.id} className="border-l-2 border-gray-200 pl-3">
                  <p className="text-sm font-medium text-gray-800">{item.toStatus.replaceAll('_', ' ')}</p>
                  <p className="text-xs text-gray-400"><DateDisplay value={item.changedAtUtc} format="date" fallback="-" /></p>
                  {item.notes && <p className="mt-1 text-xs text-gray-500">{item.notes}</p>}
                </li>
              ))}
            </ol>
          </Panel>

          <Panel title="Activity">
            <ol className="space-y-3">
              {activity.map(item => (
                <li key={item.id}>
                  <p className="text-sm text-gray-800">{item.summary}</p>
                  <p className="text-xs text-gray-400"><DateDisplay value={item.occurredAtUtc} format="date" fallback="-" /></p>
                </li>
              ))}
            </ol>
          </Panel>
        </aside>
      </div>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3">
      <p className="text-xs text-gray-400">{label}</p>
      <p className="mt-1 text-xl font-semibold text-gray-900">{value}</p>
    </div>
  );
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4">
      <h2 className="mb-3 text-sm font-semibold text-gray-900">{title}</h2>
      {children}
    </section>
  );
}
