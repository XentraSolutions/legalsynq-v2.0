'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { MOCK_CASES, formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { CASE_STATUS_LABELS } from '@/types/lien';

export default function CasesPage() {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');

  const filtered = MOCK_CASES.filter((c) => {
    if (search && !c.clientName.toLowerCase().includes(search.toLowerCase()) && !c.caseNumber.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter && c.status !== statusFilter) return false;
    return true;
  });

  return (
    <div className="space-y-5">
      <PageHeader
        title="Cases"
        subtitle={`${filtered.length} cases`}
        actions={
          <button className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />
            Add New Case
          </button>
        }
      />
      <FilterToolbar
        searchPlaceholder="Search cases by name or number..."
        onSearch={setSearch}
        filters={[
          {
            label: 'All Statuses',
            value: statusFilter,
            onChange: setStatusFilter,
            options: Object.entries(CASE_STATUS_LABELS).map(([v, l]) => ({ value: v, label: l })),
          },
        ]}
      />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead>
              <tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Client</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Law Firm</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Medical Facility</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Liens</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Total Amount</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Assigned</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((c) => (
                <tr key={c.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <Link href={`/lien/cases/${c.id}`} className="text-xs font-mono text-primary hover:underline">{c.caseNumber}</Link>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700 font-medium">{c.clientName}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">{c.lawFirm}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">{c.medicalFacility}</td>
                  <td className="px-4 py-3 text-sm text-gray-600 tabular-nums">{c.lienCount}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 font-medium tabular-nums">{formatCurrency(c.totalLienAmount)}</td>
                  <td className="px-4 py-3"><StatusBadge status={c.status} /></td>
                  <td className="px-4 py-3 text-sm text-gray-500">{c.assignedTo}</td>
                  <td className="px-4 py-3 text-right">
                    <Link href={`/lien/cases/${c.id}`} className="text-xs text-primary font-medium hover:underline">View &rarr;</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && (
          <div className="p-10 text-center text-sm text-gray-400">No cases match your filters.</div>
        )}
      </div>
    </div>
  );
}
