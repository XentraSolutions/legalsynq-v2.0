'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { SideDrawer } from '@/components/lien/side-drawer';
import { CreateLienModal } from '@/components/lien/forms/create-lien-modal';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { LIEN_TYPE_LABELS } from '@/types/lien';

export default function LiensPage() {
  const liens = useLienStore((s) => s.liens);
  const updateLien = useLienStore((s) => s.updateLien);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);

  const filtered = liens.filter((l) => {
    if (search && !l.lienNumber.toLowerCase().includes(search.toLowerCase()) && !(l.subjectParty && `${l.subjectParty.firstName} ${l.subjectParty.lastName}`.toLowerCase().includes(search.toLowerCase()))) return false;
    if (statusFilter && l.status !== statusFilter) return false;
    if (typeFilter && l.lienType !== typeFilter) return false;
    return true;
  });

  const previewLien = previewId ? liens.find((l) => l.id === previewId) : null;
  const canEdit = canPerformAction(role, 'edit');

  return (
    <div className="space-y-5">
      <PageHeader title="Liens" subtitle={`${filtered.length} liens`}
        actions={canPerformAction(role, 'create') ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />New Lien
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search liens by number or subject..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [{ value: 'Draft', label: 'Draft' }, { value: 'Offered', label: 'Offered' }, { value: 'Sold', label: 'Sold' }, { value: 'Withdrawn', label: 'Withdrawn' }] },
        { label: 'All Types', value: typeFilter, onChange: setTypeFilter, options: Object.entries(LIEN_TYPE_LABELS).map(([v, l]) => ({ value: v, label: l })) },
      ]} />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead><tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Subject</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Original</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Offer</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
              <th className="px-4 py-3" />
            </tr></thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((l) => (
                <tr key={l.id} className="hover:bg-gray-50 transition-colors cursor-pointer" onClick={() => setPreviewId(l.id)}>
                  <td className="px-4 py-3"><Link href={`/lien/liens/${l.id}`} onClick={(e) => e.stopPropagation()} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link></td>
                  <td className="px-4 py-3 text-sm text-gray-700">{LIEN_TYPE_LABELS[l.lienType] ?? l.lienType}</td>
                  <td className="px-4 py-3 text-sm text-gray-700">{l.isConfidential ? <span className="italic text-gray-400">Confidential</span> : l.subjectParty ? `${l.subjectParty.firstName} ${l.subjectParty.lastName}` : '\u2014'}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{formatCurrency(l.originalAmount)}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{formatCurrency(l.offerPrice)}</td>
                  <td className="px-4 py-3"><StatusBadge status={l.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">{formatDate(l.createdAtUtc)}</td>
                  <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                    <ActionMenu items={[
                      { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                      ...(canEdit && l.status === 'Draft' ? [{ label: 'List for Sale', icon: 'ri-price-tag-3-line', onClick: () => { updateLien(l.id, { status: 'Offered', offerPrice: Math.round(l.originalAmount * 0.8) }); addToast({ type: 'success', title: 'Lien Listed', description: `${l.lienNumber} is now offered for sale` }); } }] : []),
                      ...(canEdit && l.status === 'Offered' ? [{ label: 'Withdraw', icon: 'ri-close-circle-line', onClick: () => { updateLien(l.id, { status: 'Withdrawn' }); addToast({ type: 'warning', title: 'Lien Withdrawn', description: `${l.lienNumber} has been withdrawn` }); }, variant: 'danger' as const }] : []),
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No liens match your filters.</div>}
      </div>

      <CreateLienModal open={showCreate} onClose={() => setShowCreate(false)} />

      <SideDrawer open={!!previewLien} onClose={() => setPreviewId(null)} title={previewLien?.lienNumber || ''} subtitle={LIEN_TYPE_LABELS[previewLien?.lienType || '']}>
        {previewLien && (
          <div className="space-y-4">
            <StatusBadge status={previewLien.status} size="md" />
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div><p className="text-xs text-gray-400">Original</p><p className="font-medium text-gray-700">{formatCurrency(previewLien.originalAmount)}</p></div>
              <div><p className="text-xs text-gray-400">Offer Price</p><p className="font-medium text-blue-600">{formatCurrency(previewLien.offerPrice)}</p></div>
              <div><p className="text-xs text-gray-400">Jurisdiction</p><p className="text-gray-700">{previewLien.jurisdiction}</p></div>
              <div><p className="text-xs text-gray-400">Case Ref</p><p className="text-gray-700">{previewLien.caseRef || '\u2014'}</p></div>
            </div>
            <Link href={`/lien/liens/${previewLien.id}`} className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">View Full Details</Link>
          </div>
        )}
      </SideDrawer>
    </div>
  );
}
