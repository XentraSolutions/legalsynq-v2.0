'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { KpiCard } from '@/components/lien/kpi-card';
import { MOCK_BILLS_OF_SALE, formatCurrency, formatDate } from '@/lib/lien-mock-data';

export default function BillOfSalesPage() {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');

  const filtered = MOCK_BILLS_OF_SALE.filter((b) => {
    if (search && !b.bosNumber.toLowerCase().includes(search.toLowerCase()) && !b.lienNumber.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter && b.status !== statusFilter) return false;
    return true;
  });

  const executedCount = MOCK_BILLS_OF_SALE.filter((b) => b.status === 'Executed').length;
  const totalVolume = MOCK_BILLS_OF_SALE.filter((b) => b.status === 'Executed').reduce((s, b) => s + b.saleAmount, 0);

  return (
    <div className="space-y-5">
      <PageHeader title="Bill of Sales" subtitle={`${filtered.length} records`} actions={
        <button className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
          <i className="ri-add-line text-base" />
          New Bill of Sale
        </button>
      } />

      <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
        <KpiCard title="Total BOS" value={MOCK_BILLS_OF_SALE.length} icon="ri-receipt-line" iconColor="text-indigo-600" />
        <KpiCard title="Executed" value={executedCount} icon="ri-checkbox-circle-line" iconColor="text-green-600" />
        <KpiCard title="Pending" value={MOCK_BILLS_OF_SALE.filter((b) => b.status === 'Pending').length} icon="ri-time-line" iconColor="text-amber-600" />
        <KpiCard title="Volume" value={formatCurrency(totalVolume)} icon="ri-money-dollar-circle-line" iconColor="text-emerald-600" />
      </div>

      <FilterToolbar searchPlaceholder="Search by BOS # or Lien #..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [{ value: 'Draft', label: 'Draft' }, { value: 'Pending', label: 'Pending' }, { value: 'Executed', label: 'Executed' }, { value: 'Cancelled', label: 'Cancelled' }] },
      ]} />

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead>
              <tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">BOS #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Seller</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Buyer</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Amount</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Execution Date</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((b) => (
                <tr key={b.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3"><Link href={`/lien/bill-of-sales/${b.id}`} className="text-xs font-mono text-primary hover:underline">{b.bosNumber}</Link></td>
                  <td className="px-4 py-3 text-xs font-mono text-gray-500">{b.lienNumber}</td>
                  <td className="px-4 py-3 text-xs font-mono text-gray-500">{b.caseNumber ?? '\u2014'}</td>
                  <td className="px-4 py-3 text-sm text-gray-700">{b.sellerOrg}</td>
                  <td className="px-4 py-3 text-sm text-gray-700">{b.buyerOrg}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 font-medium tabular-nums">{formatCurrency(b.saleAmount)}</td>
                  <td className="px-4 py-3"><StatusBadge status={b.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-400">{b.executionDate ? formatDate(b.executionDate) : '\u2014'}</td>
                  <td className="px-4 py-3 text-right"><Link href={`/lien/bill-of-sales/${b.id}`} className="text-xs text-primary font-medium hover:underline">View &rarr;</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No records found.</div>}
      </div>
    </div>
  );
}
