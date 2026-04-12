'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { MOCK_LIENS, formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { LIEN_TYPE_LABELS } from '@/types/lien';

export default function LiensPage() {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');

  const filtered = MOCK_LIENS.filter((l) => {
    if (search && !l.lienNumber.toLowerCase().includes(search.toLowerCase()) && !(l.subjectParty && `${l.subjectParty.firstName} ${l.subjectParty.lastName}`.toLowerCase().includes(search.toLowerCase()))) return false;
    if (statusFilter && l.status !== statusFilter) return false;
    if (typeFilter && l.lienType !== typeFilter) return false;
    return true;
  });

  return (
    <div className="space-y-5">
      <PageHeader
        title="Liens"
        subtitle={`${filtered.length} liens`}
        actions={
          <button className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />
            New Lien
          </button>
        }
      />
      <FilterToolbar
        searchPlaceholder="Search liens by number or subject..."
        onSearch={setSearch}
        filters={[
          { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [{ value: 'Draft', label: 'Draft' }, { value: 'Offered', label: 'Offered' }, { value: 'Sold', label: 'Sold' }, { value: 'Withdrawn', label: 'Withdrawn' }] },
          { label: 'All Types', value: typeFilter, onChange: setTypeFilter, options: Object.entries(LIEN_TYPE_LABELS).map(([v, l]) => ({ value: v, label: l })) },
        ]}
      />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead>
              <tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Subject</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case Ref</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Original</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Offer</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Jurisdiction</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((l) => (
                <tr key={l.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3"><Link href={`/lien/liens/${l.id}`} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link></td>
                  <td className="px-4 py-3 text-sm text-gray-700">{LIEN_TYPE_LABELS[l.lienType] ?? l.lienType}</td>
                  <td className="px-4 py-3 text-sm text-gray-700">{l.isConfidential ? <span className="italic text-gray-400">Confidential</span> : l.subjectParty ? `${l.subjectParty.firstName} ${l.subjectParty.lastName}` : '\u2014'}</td>
                  <td className="px-4 py-3 text-xs font-mono text-gray-500">{l.caseRef ?? '\u2014'}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{formatCurrency(l.originalAmount)}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{formatCurrency(l.offerPrice)}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{l.jurisdiction ?? '\u2014'}</td>
                  <td className="px-4 py-3"><StatusBadge status={l.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">{formatDate(l.createdAtUtc)}</td>
                  <td className="px-4 py-3 text-right"><Link href={`/lien/liens/${l.id}`} className="text-xs text-primary font-medium hover:underline">View &rarr;</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No liens match your filters.</div>}
      </div>
    </div>
  );
}
