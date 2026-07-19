'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import Link from 'next/link';
import type { ColumnDef, OnChangeFn, RowSelectionState } from '@tanstack/react-table';
import { BaseTable } from '@/components/ui/base-table';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { UploadDocumentForm } from '@/components/lien/forms/upload-document-form';
import { ConfirmDialog } from '@/components/lien/modal';
import { BulkActionBar } from '@/components/lien/bulk-action-bar';
import { BulkConfirmModal } from '@/components/lien/bulk-confirm-modal';
import { BulkResultBanner } from '@/components/lien/bulk-result-banner';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useSelectionState } from '@/hooks/use-selection-state';
import { useTimezone } from '@/lib/use-timezone';
import { documentsService, type DocumentListItem } from '@/lib/documents';
import { executeBulk, type BulkActionConfig, type BulkOperationResult } from '@/lib/bulk-operations';

export const dynamic = 'force-dynamic';


const STATUS_OPTIONS = [
  { value: 'DRAFT', label: 'Draft' },
  { value: 'ACTIVE', label: 'Active' },
  { value: 'ARCHIVED', label: 'Archived' },
  { value: 'LEGAL_HOLD', label: 'Legal Hold' },
];

const STATUS_DISPLAY: Record<string, string> = {
  DRAFT: 'Pending',
  ACTIVE: 'Active',
  ARCHIVED: 'Archived',
  LEGAL_HOLD: 'Legal Hold',
};

const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: 'archive',
    label: 'Archive',
    icon: 'ri-archive-line',
    variant: 'danger',
    confirmTitle: 'Archive Documents',
    confirmDescription: (count) =>
      `This will archive ${count} document${count !== 1 ? 's' : ''}. Already archived documents will be skipped.`,
  },
];

function formatDocumentTimestamp(value: string, timezone: string): string {
  if (!value) return '—';

  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) return value;

  return timestamp.toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZone: timezone,
  });
}

export default function DocumentHandlingPage() {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const selection = useSelectionState();
  const timezone = useTimezone();

  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [showUpload, setShowUpload] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string; label: string } | null>(null);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(null);

  const fetchDocuments = useCallback(async () => {
    try {
      setLoading(true);
      const result = await documentsService.list({
        status: statusFilter || undefined,
        limit: 100,
      });
      setDocuments(result.items);
      setTotal(result.pagination.total);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load documents' });
    } finally {
      setLoading(false);
    }
  }, [statusFilter, addToast]);

  useEffect(() => { fetchDocuments(); }, [fetchDocuments]);

  const filtered = documents.filter((d) => {
    if (search) {
      const q = search.toLowerCase();
      if (!d.title.toLowerCase().includes(q) && !d.referenceId.toLowerCase().includes(q)) return false;
    }
    return true;
  });

  const canEdit = ra.can('document:edit');

  const handleStatusUpdate = async () => {
    if (!confirmAction) return;
    try {
      await documentsService.update(confirmAction.id, { status: confirmAction.status });
      addToast({ type: 'success', title: confirmAction.label });
      setConfirmAction(null);
      fetchDocuments();
    } catch (err) {
      addToast({ type: 'error', title: 'Update Failed', description: err instanceof Error ? err.message : 'Failed to update document' });
      setConfirmAction(null);
    }
  };

  const handleDownload = async (doc: DocumentListItem) => {
    try {
      const url = await documentsService.getDownloadUrl(doc.id);
      window.open(url, '_blank');
    } catch (err) {
      addToast({ type: 'error', title: 'Download Failed', description: err instanceof Error ? err.message : 'Could not generate download link' });
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
      const doc = filtered.find((d) => d.id === id);
      if (!doc) throw new Error('Document not found in current list');
      if (doc.status === 'ARCHIVED') throw new Error('Document is already archived');
      if (doc.status === 'LEGAL_HOLD') throw new Error('Document is under legal hold and cannot be archived');
      await documentsService.update(id, { status: 'ARCHIVED' });
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchDocuments();
  };

  const allIds = filtered.map((d) => d.id);

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

    const allNowSelected = nextIds.size === filtered.length && prevIds.size !== filtered.length;
    const allNowDeselected = nextIds.size === 0 && prevIds.size === filtered.length;
    if (allNowSelected || allNowDeselected) {
      selection.toggleAll(allIds);
      return;
    }

    const toggled = filtered.find((d) => prevIds.has(d.id) !== nextIds.has(d.id));
    if (toggled) selection.toggle(toggled.id);
  };

  const columns = useMemo<ColumnDef<DocumentListItem, any>[]>(
    () => [
      {
        id: 'title',
        header: 'Title',
        cell: ({ row }) => (
          <>
            <Link href={`/lien/document-handling/${row.original.id}`} className="text-sm font-medium text-primary hover:underline">
              {row.original.title}
            </Link>
            {row.original.mimeType && <p className="text-xs text-gray-400 mt-0.5">{row.original.mimeType}</p>}
          </>
        ),
      },
      {
        id: 'referenceType',
        header: 'Type',
        cell: ({ row }) => <span className="text-sm text-gray-600">{row.original.referenceType}</span>,
      },
      {
        id: 'referenceId',
        header: 'Linked To',
        cell: ({ row }) => <span className="text-xs text-gray-500">{row.original.referenceId || '—'}</span>,
      },
      {
        id: 'fileSize',
        header: 'Size',
        cell: ({ row }) => <span className="text-xs text-gray-400">{row.original.fileSize}</span>,
      },
      {
        id: 'versionCount',
        header: 'Versions',
        cell: ({ row }) => <span className="text-xs text-gray-500">v{row.original.versionCount}</span>,
      },
      {
        id: 'scanStatus',
        header: 'Scan',
        cell: ({ row }) => <StatusBadge status={row.original.scanStatus} />,
      },
      {
        id: 'status',
        header: 'Status',
        cell: ({ row }) => <StatusBadge status={STATUS_DISPLAY[row.original.status] ?? row.original.status} />,
      },
      {
        id: 'createdAt',
        header: 'Date',
        cell: ({ row }) => (
          <span className="text-xs text-gray-400 whitespace-nowrap">
            {formatDocumentTimestamp(row.original.createdAt, timezone)}
          </span>
        ),
      },
      {
        id: 'actions',
        header: '',
        meta: { align: 'right' },
        cell: ({ row }) => {
          const d = row.original;
          return (
            <ActionMenu
              items={[
                { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                { label: 'Download', icon: 'ri-download-2-line', onClick: () => handleDownload(d) },
                ...(canEdit && d.status !== 'ARCHIVED' ? [{ label: 'Archive', icon: 'ri-archive-line', onClick: () => setConfirmAction({ id: d.id, status: 'ARCHIVED', label: 'Archive Document' }), divider: true }] : []),
              ]}
            />
          );
        },
      },
    ],
    [canEdit, timezone],
  );

  return (
    <div className="space-y-5">
      <PageHeader title="Document Handling" subtitle={`${total} documents`}
        actions={ra.can('document:upload') ? (
          <button onClick={() => setShowUpload(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-upload-2-line text-base" />Upload Document
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search documents..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: STATUS_OPTIONS },
      ]} />

      <BulkResultBanner result={bulkResult} onDismiss={() => setBulkResult(null)} entityLabel="documents" />

      <BaseTable
        data={filtered}
        columns={columns}
        getRowId={(d) => d.id}
        isLoading={loading}
        emptyMessage="No documents found."
        enablePagination={false}
        rowSelection={canEdit ? rowSelection : undefined}
        onRowSelectionChange={handleRowSelectionChange}
        getRowClassName={(d) => (selection.isSelected(d.id) ? 'bg-primary/5' : undefined)}
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

      <UploadDocumentForm open={showUpload} onClose={() => setShowUpload(false)} onUploaded={fetchDocuments} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={handleStatusUpdate}
          title={confirmAction.label} description={`${confirmAction.label} for this document?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
