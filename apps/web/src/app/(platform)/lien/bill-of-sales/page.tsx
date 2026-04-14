'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { KpiCard } from '@/components/lien/kpi-card';
import { ActionMenu } from '@/components/lien/action-menu';
import { ConfirmDialog } from '@/components/lien/modal';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import {
  billOfSaleService,
  formatCurrency,
  BOS_STATUS_LABELS,
  type BillOfSaleListItem,
} from '@/lib/billofsale';

export default function BillOfSalesPage() {
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [items, setItems] = useState<BillOfSaleListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [confirmAction, setConfirmAction] = useState<{ id: string; action: string; label: string } | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const result = await billOfSaleService.getBillOfSales({
        search: search || undefined,
        status: statusFilter || undefined,
        pageSize: 100,
      });
      setItems(result.items);
      setTotalCount(result.pagination.totalCount);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load bill of sales' });
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, addToast]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const executedCount = items.filter((b) => b.status === 'Executed').length;
  const pendingCount = items.filter((b) => b.status === 'Pending').length;
  const totalVolume = items.filter((b) => b.status === 'Executed').reduce((s, b) => s + b.purchaseAmount, 0);
  const canEdit = canPerformAction(role, 'edit');

  const handleConfirmAction = async () => {
    if (!confirmAction) return;
    try {
      if (confirmAction.action === 'submit') {
        await billOfSaleService.submitForExecution(confirmAction.id);
      } else if (confirmAction.action === 'execute') {
        await billOfSaleService.execute(confirmAction.id);
      } else if (confirmAction.action === 'cancel') {
        await billOfSaleService.cancel(confirmAction.id);
      }
      addToast({ type: confirmAction.action === 'cancel' ? 'warning' : 'success', title: confirmAction.label, description: `Bill of Sale has been updated` });
      setConfirmAction(null);
      fetchData();
    } catch (err) {
      addToast({ type: 'error', title: 'Action Failed', description: err instanceof Error ? err.message : 'Failed to update status' });
      setConfirmAction(null);
    }
  };

  return (
    <div className="space-y-5">
      <PageHeader title="Bill of Sales" subtitle={`${totalCount} records`}
        actions={canPerformAction(role, 'create') ? (
          <button onClick={() => addToast({ type: 'info', title: 'Info', description: 'BOS creation is done via the lien sale flow' })} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />New Bill of Sale
          </button>
        ) : undefined}
      />

      <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
        <KpiCard title="Total BOS" value={totalCount} icon="ri-receipt-line" iconColor="text-indigo-600" />
        <KpiCard title="Executed" value={executedCount} icon="ri-checkbox-circle-line" iconColor="text-green-600" />
        <KpiCard title="Pending" value={pendingCount} icon="ri-time-line" iconColor="text-amber-600" />
        <KpiCard title="Volume" value={formatCurrency(totalVolume)} icon="ri-money-dollar-circle-line" iconColor="text-emerald-600" />
      </div>

      <FilterToolbar searchPlaceholder="Search by BOS # or keywords..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: Object.entries(BOS_STATUS_LABELS).map(([v, l]) => ({ value: v, label: l })) },
      ]} />

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="p-10 text-center text-sm text-gray-400">Loading bill of sales...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead><tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">BOS #</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Seller</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Buyer</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Amount</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Discount</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Issued</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Executed</th>
                <th className="px-4 py-3" />
              </tr></thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((b) => (
                  <tr key={b.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3"><Link href={`/lien/bill-of-sales/${b.id}`} className="text-xs font-mono text-primary hover:underline">{b.bosNumber}</Link></td>
                    <td className="px-4 py-3 text-sm text-gray-700">{b.sellerContactName || '—'}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{b.buyerContactName || '—'}</td>
                    <td className="px-4 py-3 text-sm text-gray-700 font-medium tabular-nums">{formatCurrency(b.purchaseAmount)}</td>
                    <td className="px-4 py-3 text-sm text-gray-500 tabular-nums">{b.discountPercent != null ? `${b.discountPercent.toFixed(1)}%` : '—'}</td>
                    <td className="px-4 py-3"><StatusBadge status={b.status} /></td>
                    <td className="px-4 py-3 text-xs text-gray-400">{b.issuedAt || '—'}</td>
                    <td className="px-4 py-3 text-xs text-gray-400">{b.executedAt || '—'}</td>
                    <td className="px-4 py-3 text-right">
                      <ActionMenu items={[
                        { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                        ...(canEdit && b.status === 'Draft' ? [{ label: 'Submit for Execution', icon: 'ri-send-plane-line', onClick: () => setConfirmAction({ id: b.id, action: 'submit', label: 'Submit for Execution' }) }] : []),
                        ...(canEdit && b.status === 'Pending' ? [{ label: 'Execute', icon: 'ri-checkbox-circle-line', onClick: () => setConfirmAction({ id: b.id, action: 'execute', label: 'Execute' }) }] : []),
                        ...(canEdit && (b.status === 'Draft' || b.status === 'Pending') ? [{ label: 'Cancel', icon: 'ri-close-circle-line', onClick: () => setConfirmAction({ id: b.id, action: 'cancel', label: 'Cancel' }), variant: 'danger' as const, divider: true }] : []),
                      ]} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {!loading && items.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No bill of sales found.</div>}
      </div>

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={handleConfirmAction}
          title={confirmAction.label}
          description={`Are you sure you want to ${confirmAction.label.toLowerCase()} this Bill of Sale?`}
          confirmLabel={confirmAction.label}
          confirmVariant={confirmAction.action === 'cancel' ? 'danger' : 'primary'}
        />
      )}
    </div>
  );
}
