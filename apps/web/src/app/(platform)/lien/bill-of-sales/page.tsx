'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import type { ColumnDef, OnChangeFn, RowSelectionState } from '@tanstack/react-table';
import { BaseTable } from '@/components/ui/base-table';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { KpiCard } from '@/components/lien/kpi-card';
import { ActionMenu } from '@/components/lien/action-menu';
import { ConfirmDialog } from '@/components/lien/modal';
import { BulkActionBar } from '@/components/lien/bulk-action-bar';
import { BulkConfirmModal } from '@/components/lien/bulk-confirm-modal';
import { BulkResultBanner } from '@/components/lien/bulk-result-banner';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useProviderMode } from '@/hooks/use-provider-mode';
import { useSelectionState } from '@/hooks/use-selection-state';
import {
  billOfSaleService,
  formatCurrency,
  BOS_STATUS_LABELS,
  type BillOfSaleListItem,
  type PaginationMeta,
} from '@/lib/billofsale';
import { executeBulk, type BulkActionConfig, type BulkOperationResult } from '@/lib/bulk-operations';

export const dynamic = 'force-dynamic';


const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: 'execute',
    label: 'Execute',
    icon: 'ri-checkbox-circle-line',
    variant: 'primary',
    confirmTitle: 'Execute Bill of Sales',
    confirmDescription: (count) =>
      `This will execute ${count} bill of sale${count !== 1 ? 's' : ''}. Only items in "Pending" status will be processed.`,
  },
];

export default function BillOfSalesPage() {
  const { isManageMode, isReady } = useProviderMode();
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const selection = useSelectionState();

  const [items, setItems] = useState<BillOfSaleListItem[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
  });
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [confirmAction, setConfirmAction] = useState<{ id: string; action: string; label: string } | null>(null);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(null);

  // KPI cards need totals across the full (search-filtered) result set, not just the
  // current 10-row page, so they're computed from a separate unpaginated fetch.
  const [summary, setSummary] = useState({ totalCount: 0, executedCount: 0, pendingCount: 0, totalVolume: 0 });

  const fetchData = useCallback(async (page = 1) => {
    try {
      setLoading(true);
      const result = await billOfSaleService.getBillOfSales({
        search: search || undefined,
        status: statusFilter || undefined,
        page,
        pageSize: 10,
      });
      setItems(result.items);
      setPagination(result.pagination);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load bill of sales' });
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, addToast]);

  const fetchSummary = useCallback(async () => {
    try {
      const result = await billOfSaleService.getBillOfSales({
        search: search || undefined,
        pageSize: 1000,
      });
      setSummary({
        totalCount: result.pagination.totalCount,
        executedCount: result.items.filter((b) => b.status === 'Executed').length,
        pendingCount: result.items.filter((b) => b.status === 'Pending').length,
        totalVolume: result.items.filter((b) => b.status === 'Executed').reduce((s, b) => s + b.purchaseAmount, 0),
      });
    } catch {
      // KPI cards are supplementary; a failed summary fetch shouldn't block the table.
    }
  }, [search]);

  useEffect(() => {
    if (isReady && isManageMode) {
      router.replace('/lien/dashboard');
      return;
    }
    if (isReady && !isManageMode) {
      fetchData(1);
      fetchSummary();
    }
  }, [fetchData, fetchSummary, isReady, isManageMode, router]);

  const canEdit = ra.can('bos:manage');

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
      fetchData(pagination.page);
      fetchSummary();
    } catch (err) {
      addToast({ type: 'error', title: 'Action Failed', description: err instanceof Error ? err.message : 'Failed to update status' });
      setConfirmAction(null);
    }
  };

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const bos = items.find((b) => b.id === id);
      if (!bos) throw new Error('Bill of Sale not found in current list');
      if (bos.status !== 'Pending') throw new Error(`Bill of Sale is "${bos.status}" — only Pending items can be executed`);
      await billOfSaleService.execute(id);
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchData(pagination.page);
    fetchSummary();
  };

  const allIds = items.map((b) => b.id);

  const rowSelection = useMemo<RowSelectionState>(() => {
    const obj: RowSelectionState = {};
    selection.selectedIds.forEach((id) => {
      obj[id] = true;
    });
    return obj;
  }, [selection.selectedIds]);

  const handleRowSelectionChange: OnChangeFn<RowSelectionState> = (updater) => {
    const next = typeof updater === 'function' ? updater(rowSelection) : updater;
    const nextIds = new Set(Object.keys(next).filter((id) => next[id]));
    const prevIds = selection.selectedIds;

    if (nextIds.size === prevIds.size) return;

    const allNowSelected = nextIds.size === items.length && prevIds.size !== items.length;
    const allNowDeselected = nextIds.size === 0 && prevIds.size === items.length;
    if (allNowSelected || allNowDeselected) {
      selection.toggleAll(allIds);
      return;
    }

    const toggled = items.find((b) => prevIds.has(b.id) !== nextIds.has(b.id));
    if (toggled) selection.toggle(toggled.id);
  };

  const columns = useMemo<ColumnDef<BillOfSaleListItem, any>[]>(
    () => [
      {
        id: 'bosNumber',
        header: 'BOS #',
        cell: ({ row }) => (
          <Link href={`/lien/bill-of-sales/${row.original.id}`} className="text-xs font-mono text-primary hover:underline">
            {row.original.bosNumber}
          </Link>
        ),
      },
      {
        id: 'sellerContactName',
        header: 'Seller',
        cell: ({ row }) => <span className="text-sm text-gray-700">{row.original.sellerContactName || '—'}</span>,
      },
      {
        id: 'buyerContactName',
        header: 'Buyer',
        cell: ({ row }) => <span className="text-sm text-gray-700">{row.original.buyerContactName || '—'}</span>,
      },
      {
        id: 'purchaseAmount',
        header: 'Amount',
        cell: ({ row }) => (
          <span className="text-sm text-gray-700 font-medium tabular-nums">{formatCurrency(row.original.purchaseAmount)}</span>
        ),
      },
      {
        id: 'discountPercent',
        header: 'Discount',
        cell: ({ row }) => (
          <span className="text-sm text-gray-500 tabular-nums">
            {row.original.discountPercent != null ? `${row.original.discountPercent.toFixed(1)}%` : '—'}
          </span>
        ),
      },
      {
        id: 'status',
        header: 'Status',
        cell: ({ row }) => <StatusBadge status={row.original.status} />,
      },
      {
        id: 'issuedAt',
        header: 'Issued',
        cell: ({ row }) => <span className="text-xs text-gray-400">{row.original.issuedAt || '—'}</span>,
      },
      {
        id: 'executedAt',
        header: 'Executed',
        cell: ({ row }) => <span className="text-xs text-gray-400">{row.original.executedAt || '—'}</span>,
      },
      {
        id: 'actions',
        header: '',
        meta: { align: 'right' },
        cell: ({ row }) => {
          const b = row.original;
          return (
            <ActionMenu
              items={[
                { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                ...(canEdit && b.status === 'Draft' ? [{ label: 'Submit for Execution', icon: 'ri-send-plane-line', onClick: () => setConfirmAction({ id: b.id, action: 'submit', label: 'Submit for Execution' }) }] : []),
                ...(canEdit && b.status === 'Pending' ? [{ label: 'Execute', icon: 'ri-checkbox-circle-line', onClick: () => setConfirmAction({ id: b.id, action: 'execute', label: 'Execute' }) }] : []),
                ...(canEdit && (b.status === 'Draft' || b.status === 'Pending') ? [{ label: 'Cancel', icon: 'ri-close-circle-line', onClick: () => setConfirmAction({ id: b.id, action: 'cancel', label: 'Cancel' }), variant: 'danger' as const, divider: true }] : []),
              ]}
            />
          );
        },
      },
    ],
    [canEdit],
  );

  if (!isReady || isManageMode) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <p className="text-sm text-gray-400 mt-2">Loading...</p>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <PageHeader title="Bill of Sales" subtitle={`${summary.totalCount} records`}
        actions={ra.can('bos:manage') ? (
          <button onClick={() => addToast({ type: 'info', title: 'Info', description: 'BOS creation is done via the lien sale flow' })} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />New Bill of Sale
          </button>
        ) : undefined}
      />

      {/* TODO: replace this client-side summary fetch (pageSize: 1000) with a dedicated
          backend summary/stats endpoint once available, so KPI totals aren't capped. */}
      <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
        <KpiCard title="Total BOS" value={summary.totalCount} icon="ri-receipt-line" iconColor="text-indigo-600" />
        <KpiCard title="Executed" value={summary.executedCount} icon="ri-checkbox-circle-line" iconColor="text-green-600" />
        <KpiCard title="Pending" value={summary.pendingCount} icon="ri-time-line" iconColor="text-amber-600" />
        <KpiCard title="Volume" value={formatCurrency(summary.totalVolume)} icon="ri-money-dollar-circle-line" iconColor="text-emerald-600" />
      </div>

      <FilterToolbar searchPlaceholder="Search by BOS # or keywords..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: Object.entries(BOS_STATUS_LABELS).map(([v, l]) => ({ value: v, label: l })) },
      ]} />

      <BulkResultBanner result={bulkResult} onDismiss={() => setBulkResult(null)} entityLabel="bill of sales" />

      <BaseTable
        data={items}
        columns={columns}
        getRowId={(b) => b.id}
        isLoading={loading}
        emptyMessage="No bill of sales found."
        manualPagination
        pageCount={pagination.totalPages}
        totalCount={pagination.totalCount}
        pagination={{ pageIndex: pagination.page - 1, pageSize: pagination.pageSize }}
        onPaginationChange={(updater) => {
          const next =
            typeof updater === 'function'
              ? updater({ pageIndex: pagination.page - 1, pageSize: pagination.pageSize })
              : updater;
          fetchData(next.pageIndex + 1);
        }}
        rowSelection={canEdit ? rowSelection : undefined}
        onRowSelectionChange={handleRowSelectionChange}
        getRowClassName={(b) => (selection.isSelected(b.id) ? 'bg-primary/5' : undefined)}
        className="bg-white border-gray-200 rounded-xl"
      />

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
