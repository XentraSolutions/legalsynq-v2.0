'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { PageHeader } from '@/components/lien/page-header';
import { lienSalesService } from '@/lib/liens/lien-sales.service';
import type { SellingPortfolioDto } from '@/lib/liens/lien-sales.types';

function money(value?: number | null): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(value ?? 0);
}

function date(value?: string | null): string {
  return value ? new Date(value).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '-';
}

function StatusBadge({ status }: { status: string }) {
  const palette = status === 'PUBLISHED'
    ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
    : status === 'WITHDRAWN' || status === 'CLOSED'
      ? 'bg-gray-100 text-gray-600 border-gray-200'
      : 'bg-amber-50 text-amber-700 border-amber-200';
  return <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${palette}`}>{status.replaceAll('_', ' ')}</span>;
}

export function LienSalesClient() {
  const [items, setItems] = useState<SellingPortfolioDto[]>([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError(null);
      try {
        const result = await lienSalesService.list({ search, status, pageSize: 50 });
        if (!cancelled) setItems(result.items);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load lien sales.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    load();
    return () => { cancelled = true; };
  }, [search, status]);

  const stats = useMemo(() => ({
    count: items.length,
    balance: items.reduce((sum, item) => sum + (item.currentBalanceTotal ?? 0), 0),
    published: items.filter(item => item.status === 'PUBLISHED').length,
  }), [items]);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Lien Sales"
        subtitle="Seller receivable portfolios built from existing liens and canonical case links."
        actions={(
          <Link href="/lien/sales/new" className="inline-flex items-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-medium text-white hover:bg-primary/90">
            <i className="ri-add-line" />
            New Portfolio
          </Link>
        )}
      />

      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <Metric label="Portfolios" value={String(stats.count)} />
        <Metric label="Outstanding Balance" value={money(stats.balance)} />
        <Metric label="Published" value={String(stats.published)} />
      </div>

      <div className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-white p-3 md:flex-row">
        <input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search name or portfolio number"
          className="min-h-10 flex-1 rounded-md border border-gray-200 px-3 text-sm outline-none focus:border-primary"
        />
        <select
          value={status}
          onChange={(event) => setStatus(event.target.value)}
          className="min-h-10 rounded-md border border-gray-200 px-3 text-sm outline-none focus:border-primary"
        >
          <option value="">All statuses</option>
          {['DRAFT', 'READY_FOR_REVIEW', 'PUBLISHED', 'UNDER_REVIEW', 'ACCEPTED', 'REJECTED', 'WITHDRAWN', 'CLOSED'].map(value => (
            <option key={value} value={value}>{value.replaceAll('_', ' ')}</option>
          ))}
        </select>
      </div>

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

      <div className="overflow-hidden rounded-lg border border-gray-200 bg-white">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                {['Portfolio', 'Status', 'Liens', 'Receivables', 'Outstanding', 'Published', ''].map(label => (
                  <th key={label} className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wide text-gray-500">{label}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading && <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-gray-400">Loading portfolios...</td></tr>}
              {!loading && items.length === 0 && <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-gray-400">No sale portfolios found.</td></tr>}
              {!loading && items.map(item => (
                <tr key={item.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3">
                    <Link href={`/lien/sales/${item.id}`} className="text-sm font-medium text-primary hover:underline">{item.name}</Link>
                    <p className="mt-0.5 text-xs font-mono text-gray-400">{item.portfolioNumber}</p>
                  </td>
                  <td className="px-4 py-3"><StatusBadge status={item.status} /></td>
                  <td className="px-4 py-3 text-sm text-gray-700">{item.lienCount}</td>
                  <td className="px-4 py-3 text-sm text-gray-700">{money(item.originalAmountTotal)}</td>
                  <td className="px-4 py-3 text-sm font-medium text-gray-900">{money(item.currentBalanceTotal)}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{date(item.publishedAtUtc)}</td>
                  <td className="px-4 py-3 text-right">
                    <Link href={`/lien/sales/${item.id}`} className="text-sm font-medium text-primary hover:underline">Open</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
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
