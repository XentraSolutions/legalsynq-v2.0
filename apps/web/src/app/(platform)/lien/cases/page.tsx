'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { SideDrawer } from '@/components/lien/side-drawer';
import { ConfirmDialog } from '@/components/lien/modal';
import { CreateCaseForm } from '@/components/lien/forms/create-case-form';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { CASE_STATUS_LABELS } from '@/types/lien';

const STATUSES = ['PreDemand', 'DemandSent', 'InNegotiation', 'CaseSettled', 'Closed'];

export default function CasesPage() {
  const cases = useLienStore((s) => s.cases);
  const updateCase = useLienStore((s) => s.updateCase);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string } | null>(null);

  const filtered = cases.filter((c) => {
    if (search && !c.caseNumber.toLowerCase().includes(search.toLowerCase()) && !c.clientName.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter && c.status !== statusFilter) return false;
    return true;
  });

  const previewCase = previewId ? cases.find((c) => c.id === previewId) : null;
  const canEdit = canPerformAction(role, 'edit');

  return (
    <div className="space-y-5">
      <PageHeader title="Cases" subtitle={`${filtered.length} cases`}
        actions={canPerformAction(role, 'create') ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />Create Case
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search by case number or client name..." onSearch={setSearch} filters={[{
        label: 'All Statuses', value: statusFilter, onChange: setStatusFilter,
        options: STATUSES.map((s) => ({ value: s, label: CASE_STATUS_LABELS[s] || s })),
      }]} />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead><tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case #</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Client</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Law Firm</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Liens</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Amount</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Assigned</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3" />
            </tr></thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((c) => (
                <tr key={c.id} className="hover:bg-gray-50 transition-colors cursor-pointer" onClick={() => setPreviewId(c.id)}>
                  <td className="px-4 py-3"><Link href={`/lien/cases/${c.id}`} onClick={(e) => e.stopPropagation()} className="text-xs font-mono text-primary hover:underline">{c.caseNumber}</Link></td>
                  <td className="px-4 py-3 text-sm text-gray-700">{c.clientName}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">{c.lawFirm}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{c.lienCount}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 font-medium tabular-nums">{formatCurrency(c.totalLienAmount)}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{c.assignedTo}</td>
                  <td className="px-4 py-3"><StatusBadge status={c.status} /></td>
                  <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                    <ActionMenu items={[
                      { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                      ...(canEdit ? [
                        { label: 'Advance Status', icon: 'ri-arrow-right-line', onClick: () => {
                          const idx = STATUSES.indexOf(c.status);
                          if (idx < STATUSES.length - 1) setConfirmAction({ id: c.id, status: STATUSES[idx + 1] });
                        }},
                        { label: 'Reassign', icon: 'ri-user-shared-line', onClick: () => {
                          const assignees = ['Sarah Chen', 'Michael Park', 'Lisa Wang'];
                          const next = assignees.find((a) => a !== c.assignedTo) || assignees[0];
                          updateCase(c.id, { assignedTo: next });
                          addToast({ type: 'success', title: 'Case Reassigned', description: `Now assigned to ${next}` });
                        }},
                      ] : []),
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No cases match your filters.</div>}
      </div>

      <CreateCaseForm open={showCreate} onClose={() => setShowCreate(false)} />

      <SideDrawer open={!!previewCase} onClose={() => setPreviewId(null)} title={previewCase?.caseNumber || ''} subtitle={previewCase?.clientName}>
        {previewCase && (
          <div className="space-y-4">
            <StatusBadge status={previewCase.status} size="md" />
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div><p className="text-xs text-gray-400">Law Firm</p><p className="text-gray-700">{previewCase.lawFirm}</p></div>
              <div><p className="text-xs text-gray-400">Facility</p><p className="text-gray-700">{previewCase.medicalFacility}</p></div>
              <div><p className="text-xs text-gray-400">Liens</p><p className="text-gray-700">{previewCase.lienCount}</p></div>
              <div><p className="text-xs text-gray-400">Amount</p><p className="font-medium text-gray-700">{formatCurrency(previewCase.totalLienAmount)}</p></div>
              <div><p className="text-xs text-gray-400">Assigned</p><p className="text-gray-700">{previewCase.assignedTo}</p></div>
              <div><p className="text-xs text-gray-400">Incident</p><p className="text-gray-700">{formatDate(previewCase.dateOfIncident)}</p></div>
            </div>
            <Link href={`/lien/cases/${previewCase.id}`} className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">View Full Details</Link>
          </div>
        )}
      </SideDrawer>

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => { updateCase(confirmAction.id, { status: confirmAction.status }); addToast({ type: 'success', title: 'Status Updated', description: `Case moved to ${CASE_STATUS_LABELS[confirmAction.status]}` }); setConfirmAction(null); }}
          title="Change Case Status" description={`Move this case to "${CASE_STATUS_LABELS[confirmAction.status]}"?`} confirmLabel="Update Status"
        />
      )}
    </div>
  );
}
