'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { SideDrawer } from '@/components/lien/side-drawer';
import { ConfirmDialog } from '@/components/lien/modal';
import { CreateCaseForm } from '@/components/lien/forms/create-case-form';
import { BulkActionBar } from '@/components/lien/bulk-action-bar';
import { BulkConfirmModal } from '@/components/lien/bulk-confirm-modal';
import { BulkResultBanner } from '@/components/lien/bulk-result-banner';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useSelectionState } from '@/hooks/use-selection-state';
import { casesService, type CaseListItem, type PaginationMeta } from '@/lib/cases';
import { executeBulk, type BulkActionConfig, type BulkOperationResult } from '@/lib/bulk-operations';
import { ApiError } from '@/lib/api-client';

const STATUSES = ['PreDemand', 'DemandSent', 'InNegotiation', 'CaseSettled', 'Closed'];
const STATUS_LABELS: Record<string, string> = {
  PreDemand: 'Pre-Demand',
  DemandSent: 'Demand Sent',
  InNegotiation: 'In Negotiation',
  CaseSettled: 'Case Settled',
  Closed: 'Closed',
};

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return '-';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: 'advance-status',
    label: 'Advance Status',
    icon: 'ri-arrow-right-line',
    variant: 'primary',
    confirmTitle: 'Advance Case Status',
    confirmDescription: (count) =>
      `This will advance ${count} case${count !== 1 ? 's' : ''} to their next status. Cases already at "Closed" will be skipped.`,
  },
];

export default function CasesPage() {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const selection = useSelectionState();

  const [cases, setCases] = useState<CaseListItem[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({ page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string } | null>(null);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(null);

  const fetchCases = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await casesService.getCases({
        search: search || undefined,
        status: statusFilter || undefined,
        page: pagination.page,
        pageSize: 20,
      });
      setCases(result.items);
      setPagination(result.pagination);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load cases');
      }
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, pagination.page]);

  useEffect(() => {
    fetchCases();
  }, [fetchCases]);

  const previewCase = previewId ? cases.find((c) => c.id === previewId) : null;
  const canEdit = ra.can('case:edit');

  const handleAdvanceStatus = async (caseItem: CaseListItem) => {
    const idx = STATUSES.indexOf(caseItem.status);
    if (idx < STATUSES.length - 1) {
      setConfirmAction({ id: caseItem.id, status: STATUSES[idx + 1] });
    }
  };

  const confirmStatusChange = async () => {
    if (!confirmAction) return;
    try {
      await casesService.updateCaseStatus(confirmAction.id, confirmAction.status);
      addToast({ type: 'success', title: 'Status Updated', description: `Case moved to ${STATUS_LABELS[confirmAction.status]}` });
      setConfirmAction(null);
      fetchCases();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to update status';
      addToast({ type: 'error', title: 'Update Failed', description: message });
      setConfirmAction(null);
    }
  };

  const handleCaseCreated = () => {
    setShowCreate(false);
    fetchCases();
  };

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const caseItem = cases.find((c) => c.id === id);
      if (!caseItem) throw new Error('Case not found in current list');
      const idx = STATUSES.indexOf(caseItem.status);
      if (idx >= STATUSES.length - 1) throw new Error(`Case is already "${STATUS_LABELS[caseItem.status] || caseItem.status}"`);
      await casesService.updateCaseStatus(id, STATUSES[idx + 1]);
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchCases();
  };

  const allIds = cases.map((c) => c.id);

  return (
    <div className="space-y-5">
      <PageHeader title="Cases" subtitle={loading ? 'Loading...' : `${pagination.totalCount} cases`}
        actions={ra.can('case:create') ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />Create Case
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search by case number or client name..." onSearch={setSearch} filters={[{
        label: 'All Statuses', value: statusFilter, onChange: setStatusFilter,
        options: STATUSES.map((s) => ({ value: s, label: STATUS_LABELS[s] || s })),
      }]} />

      <BulkResultBanner result={bulkResult} onDismiss={() => setBulkResult(null)} entityLabel="cases" />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-center gap-2">
          <i className="ri-error-warning-line text-red-500" />
          <p className="text-sm text-red-700">{error}</p>
          <button onClick={fetchCases} className="ml-auto text-sm text-red-600 hover:underline font-medium">Retry</button>
        </div>
      )}

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="p-10 text-center">
            <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <p className="text-sm text-gray-400 mt-2">Loading cases...</p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100">
                <thead><tr className="bg-gray-50">
                  {canEdit && (
                    <th className="px-4 py-3 w-10">
                      <input type="checkbox" checked={selection.isAllSelected(allIds)} onChange={() => selection.toggleAll(allIds)}
                        className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                    </th>
                  )}
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case #</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Client</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Title / Ref</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Insurance</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Demand</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Incident</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3" />
                </tr></thead>
                <tbody className="divide-y divide-gray-100">
                  {cases.map((c) => (
                    <tr key={c.id} className={`hover:bg-gray-50 transition-colors cursor-pointer ${selection.isSelected(c.id) ? 'bg-primary/5' : ''}`} onClick={() => setPreviewId(c.id)}>
                      {canEdit && (
                        <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                          <input type="checkbox" checked={selection.isSelected(c.id)} onChange={() => selection.toggle(c.id)}
                            className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                        </td>
                      )}
                      <td className="px-4 py-3"><Link href={`/lien/cases/${c.id}`} onClick={(e) => e.stopPropagation()} className="text-xs font-mono text-primary hover:underline">{c.caseNumber}</Link></td>
                      <td className="px-4 py-3 text-sm text-gray-700">{c.clientName}</td>
                      <td className="px-4 py-3 text-sm text-gray-600">{c.title || '-'}</td>
                      <td className="px-4 py-3 text-sm text-gray-600">{c.insuranceCarrier || '-'}</td>
                      <td className="px-4 py-3 text-sm text-gray-700 font-medium tabular-nums">{formatCurrency(c.demandAmount)}</td>
                      <td className="px-4 py-3 text-sm text-gray-500">{c.dateOfIncident || '-'}</td>
                      <td className="px-4 py-3"><StatusBadge status={c.status} /></td>
                      <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                        <ActionMenu items={[
                          { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                          ...(canEdit ? [
                            { label: 'Advance Status', icon: 'ri-arrow-right-line', onClick: () => handleAdvanceStatus(c) },
                          ] : []),
                        ]} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {cases.length === 0 && !loading && <div className="p-10 text-center text-sm text-gray-400">No cases match your filters.</div>}
          </>
        )}
      </div>

      {pagination.totalPages > 1 && (
        <div className="flex items-center justify-between px-1">
          <p className="text-sm text-gray-500">Page {pagination.page} of {pagination.totalPages}</p>
          <div className="flex gap-2">
            <button
              onClick={() => setPagination((p) => ({ ...p, page: p.page - 1 }))}
              disabled={pagination.page <= 1}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >Previous</button>
            <button
              onClick={() => setPagination((p) => ({ ...p, page: p.page + 1 }))}
              disabled={pagination.page >= pagination.totalPages}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >Next</button>
          </div>
        </div>
      )}

      {canEdit && (
        <BulkActionBar count={selection.count} actions={BULK_ACTIONS} onAction={handleBulkAction} onClear={selection.clear} />
      )}

      {bulkAction && (
        <BulkConfirmModal
          open
          onClose={() => setBulkAction(null)}
          onConfirm={executeBulkAction}
          title={bulkAction.confirmTitle}
          description={bulkAction.confirmDescription(selection.count)}
          count={selection.count}
          variant={bulkAction.variant}
          loading={bulkLoading}
        />
      )}

      <CreateCaseForm open={showCreate} onClose={() => setShowCreate(false)} onCreated={handleCaseCreated} />

      <SideDrawer open={!!previewCase} onClose={() => setPreviewId(null)} title={previewCase?.caseNumber || ''} subtitle={previewCase?.clientName}>
        {previewCase && (
          <div className="space-y-4">
            <StatusBadge status={previewCase.status} size="md" />
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div><p className="text-xs text-gray-400">Title</p><p className="text-gray-700">{previewCase.title || '-'}</p></div>
              <div><p className="text-xs text-gray-400">Insurance</p><p className="text-gray-700">{previewCase.insuranceCarrier || '-'}</p></div>
              <div><p className="text-xs text-gray-400">Demand</p><p className="font-medium text-gray-700">{formatCurrency(previewCase.demandAmount)}</p></div>
              <div><p className="text-xs text-gray-400">Incident</p><p className="text-gray-700">{previewCase.dateOfIncident || '-'}</p></div>
            </div>
            <Link href={`/lien/cases/${previewCase.id}`} className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">View Full Details</Link>
          </div>
        )}
      </SideDrawer>

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={confirmStatusChange}
          title="Change Case Status" description={`Move this case to "${STATUS_LABELS[confirmAction.status]}"?`} confirmLabel="Update Status"
        />
      )}
    </div>
  );
}
